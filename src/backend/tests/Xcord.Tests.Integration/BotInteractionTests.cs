using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Integration tests for bot slash command registration and execution, and
/// interaction callback dispatch.
///
/// Flow under test:
///   Admin creates bot → bot registers a command on a server → user executes it
///   → outbox event "Bot_CommandExecuted" is persisted.
///
/// Interaction callback:
///   Bot posts a callback to /api/v1/interactions/{token}/callback
///   → returns ok/deferred depending on callback type.
/// </summary>
[Collection("WebApp")]
public class BotInteractionTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public BotInteractionTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helpers ────────────

    /// <summary>
    /// Promotes a user to admin via direct DB update and re-logs in to obtain an
    /// admin JWT.
    /// </summary>
    private async Task<AuthenticatedUser> RegisterAdminAsync()
    {
        var user = await _helper.RegisterUserAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            var dbUser = await db.Users.FirstAsync(u => u.Id == user.UserId);
            dbUser.IsAdmin = true;
            await db.SaveChangesAsync();
        }

        var loginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email = user.Email,
            password = user.Password
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "admin re-login should succeed");

        return new AuthenticatedUser
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            Password = user.Password,
            AccessToken = TestHelper.ExtractAccessTokenFromCookies(loginResponse)
        };
    }

    /// <summary>
    /// Creates a bot via the admin endpoint and returns the raw token alongside the response.
    /// </summary>
    private async Task<(JsonElement body, string rawToken)> CreateBotAsync(
        string adminToken, string? username = null)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        username ??= $"bot_{id}";

        var response = await _helper.AuthPostAsync(
            "/api/v1/admin/bots",
            adminToken,
            new
            {
                username,
                displayName = $"Test Bot {id}",
                tokenName = $"default-{id}",
                permissions = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"bot creation should succeed: {await response.Content.ReadAsStringAsync()}");

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var rawToken = body.GetProperty("rawToken").GetString()!;
        return (body, rawToken);
    }

    /// <summary>
    /// Sends a request using "Authorization: Bot {rawToken}" (bot authentication scheme).
    /// </summary>
    private HttpRequestMessage BotRequest(HttpMethod method, string url, string rawToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", rawToken);
        return request;
    }

    private async Task<HttpResponseMessage> BotGetAsync(string url, string rawToken)
    {
        var request = BotRequest(HttpMethod.Get, url, rawToken);
        return await _fixture.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> BotPostAsync(string url, string rawToken, object? body = null)
    {
        var request = BotRequest(HttpMethod.Post, url, rawToken);
        if (body != null)
            request.Content = System.Net.Http.Json.JsonContent.Create(body);
        return await _fixture.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> BotDeleteAsync(string url, string rawToken)
    {
        var request = BotRequest(HttpMethod.Delete, url, rawToken);
        return await _fixture.Client.SendAsync(request);
    }

    // ──────────── Context record ────────────

    private sealed record BotCommandContext(
        AuthenticatedUser Admin,
        AuthenticatedUser Owner,
        long ServerId,
        long BotUserId,
        string BotRawToken);

    /// <summary>
    /// Sets up a full test context: admin, regular user, server, and a bot with a raw token.
    /// </summary>
    private async Task<BotCommandContext> SetupBotContextAsync()
    {
        var admin = await RegisterAdminAsync();
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var (botBody, rawToken) = await CreateBotAsync(admin.AccessToken);
        var botUserId = botBody.GetProperty("userId").ReadLong();

        return new BotCommandContext(admin, owner, serverId, botUserId, rawToken);
    }

    // ──────────── Register Command ────────────

    [Fact]
    public async Task RegisterCommand_AsBot_ReturnsCreatedCommand()
    {
        var ctx = await SetupBotContextAsync();
        var commandName = $"ping_{Guid.NewGuid():N}"[..16];

        var response = await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new
            {
                name = commandName,
                description = "A test command",
                optionsJson = (string?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"command registration should succeed: {await response.Content.ReadAsStringAsync()}");

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("serverId").ReadLong().Should().Be(ctx.ServerId);
        body.GetProperty("name").GetString().Should().Be(commandName);
        body.GetProperty("description").GetString().Should().Be("A test command");
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RegisterCommand_WithOptionsJson_ReturnsCommandWithOptions()
    {
        var ctx = await SetupBotContextAsync();
        var commandName = $"greet_{Guid.NewGuid():N}"[..16];
        var optionsJson = """[{"name":"user","type":6,"description":"The user to greet","required":true}]""";

        var response = await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new
            {
                name = commandName,
                description = "Greets a user",
                optionsJson
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("optionsJson").GetString().Should().Be(optionsJson);
    }

    [Fact]
    public async Task RegisterCommand_AsRegularUser_Returns403()
    {
        var ctx = await SetupBotContextAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.Owner.AccessToken,
            new { name = "forbidden", description = "Should not work" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RegisterCommand_Unauthenticated_Returns401()
    {
        var ctx = await SetupBotContextAsync();

        var response = await _fixture.Client.PostJsonAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            new { name = "unauth", description = "Should not work" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterCommand_EmptyName_Returns400()
    {
        var ctx = await SetupBotContextAsync();

        var response = await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new { name = "", description = "Bad command" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterCommand_DuplicateName_Returns409()
    {
        var ctx = await SetupBotContextAsync();
        var commandName = $"dup_{Guid.NewGuid():N}"[..16];

        // Register once
        var firstResponse = await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new { name = commandName, description = "First registration" });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Attempt duplicate
        var secondResponse = await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new { name = commandName, description = "Duplicate" });

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RegisterCommand_NameTooLong_Returns400()
    {
        var ctx = await SetupBotContextAsync();
        var longName = new string('a', 33); // Max is 32 chars

        var response = await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new { name = longName, description = "Too long name" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── List Commands ────────────

    [Fact]
    public async Task ListCommands_AsUser_ReturnsRegisteredCommands()
    {
        var ctx = await SetupBotContextAsync();
        var commandName = $"list_{Guid.NewGuid():N}"[..16];

        // Register a command as bot
        await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new { name = commandName, description = "List test command" });

        // List commands as a user (owner of the server)
        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var commands = body.EnumerateArray().ToList();
        commands.Should().HaveCountGreaterThanOrEqualTo(1);

        var names = commands.Select(c => c.GetProperty("name").GetString()).ToList();
        names.Should().Contain(commandName);
    }

    [Fact]
    public async Task ListCommands_AsBot_ReturnsRegisteredCommands()
    {
        var ctx = await SetupBotContextAsync();
        var commandName = $"listbot_{Guid.NewGuid():N}"[..16];

        // Register a command
        await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new { name = commandName, description = "Bot list test" });

        // List commands as the bot itself
        var response = await BotGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var names = body.EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
        names.Should().Contain(commandName);
    }

    // ──────────── Execute Command ────────────

    [Fact]
    public async Task ExecuteCommand_AsUser_CreatesOutboxEvent()
    {
        var ctx = await SetupBotContextAsync();
        var commandName = $"exec_{Guid.NewGuid():N}"[..16];

        // Register command as bot
        var registerResponse = await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new { name = commandName, description = "Executable command" });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var registerBody = await registerResponse.ReadAsJsonAsync<JsonElement>();
        var commandId = registerBody.GetProperty("id").ReadLong();

        // Execute the command as a regular user
        var executeResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands/{commandId}/execute",
            ctx.Owner.AccessToken,
            new { argsJson = (string?)null });

        executeResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"command execution should succeed: {await executeResponse.Content.ReadAsStringAsync()}");

        var executeBody = await executeResponse.ReadAsJsonAsync<JsonElement>();
        executeBody.GetProperty("commandId").ReadLong().Should().Be(commandId);
        executeBody.GetProperty("status").GetString().Should().Be("dispatched");

        // Verify the outbox event was persisted
        await using var db = _fixture.CreateDbContext();
        var outboxEvent = await db.OutboxEvents
            .Where(e => e.EventType == "Bot_CommandExecuted")
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

        outboxEvent.Should().NotBeNull("executing a command should create a Bot_CommandExecuted outbox event");
        outboxEvent!.Payload.Should().Contain(commandId.ToString(),
            "outbox payload should contain the command ID");
        outboxEvent.Payload.Should().Contain(commandName,
            "outbox payload should contain the command name");
        outboxEvent.Payload.Should().Contain(ctx.ServerId.ToString(),
            "outbox payload should contain the server ID");
    }

    [Fact]
    public async Task ExecuteCommand_WithArgsJson_CreatesOutboxEventWithArgs()
    {
        var ctx = await SetupBotContextAsync();
        var commandName = $"args_{Guid.NewGuid():N}"[..16];
        var argsJson = """{"user":"12345","message":"hello"}""";

        // Register command
        var registerResponse = await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new { name = commandName, description = "Command with args" });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var registerBody = await registerResponse.ReadAsJsonAsync<JsonElement>();
        var commandId = registerBody.GetProperty("id").ReadLong();

        // Execute with args
        var executeResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands/{commandId}/execute",
            ctx.Owner.AccessToken,
            new { argsJson });

        executeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify args appear in outbox payload
        await using var db = _fixture.CreateDbContext();
        var outboxEvent = await db.OutboxEvents
            .Where(e => e.EventType == "Bot_CommandExecuted")
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

        outboxEvent.Should().NotBeNull();
        outboxEvent!.Payload.Should().Contain("argsJson",
            "outbox payload should include the args field");
        // argsJson is stored as an escaped JSON string inside the payload,
        // so keys like "user" appear as \"user\" — check for the unquoted values
        outboxEvent.Payload.Should().Contain("user",
            "outbox payload should preserve the user argument key");
        outboxEvent.Payload.Should().Contain("12345",
            "outbox payload should preserve the user argument value");
        outboxEvent.Payload.Should().Contain("hello",
            "outbox payload should preserve the message argument value");
    }

    [Fact]
    public async Task ExecuteCommand_NonExistentCommand_Returns404()
    {
        var ctx = await SetupBotContextAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands/999999999999/execute",
            ctx.Owner.AccessToken,
            new { argsJson = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExecuteCommand_Unauthenticated_Returns401()
    {
        var ctx = await SetupBotContextAsync();

        var response = await _fixture.Client.PostJsonAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands/123456789/execute",
            new { argsJson = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExecuteCommand_WrongServer_Returns404()
    {
        var ctx = await SetupBotContextAsync();
        var commandName = $"ws_{Guid.NewGuid():N}"[..10];

        // Register command on the correct server
        var registerResponse = await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new { name = commandName, description = "Wrong server test" });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var registerBody = await registerResponse.ReadAsJsonAsync<JsonElement>();
        var commandId = registerBody.GetProperty("id").ReadLong();

        // Try to execute on a different (non-existent) server
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/999999999999/commands/{commandId}/execute",
            ctx.Owner.AccessToken,
            new { argsJson = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Delete Command ────────────

    [Fact]
    public async Task DeleteCommand_AsOwningBot_Returns200()
    {
        var ctx = await SetupBotContextAsync();
        var commandName = $"del_{Guid.NewGuid():N}"[..16];

        // Register a command
        var registerResponse = await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new { name = commandName, description = "Delete me" });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var registerBody = await registerResponse.ReadAsJsonAsync<JsonElement>();
        var commandId = registerBody.GetProperty("id").ReadLong();

        // Delete the command
        var deleteResponse = await BotDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands/{commandId}",
            ctx.BotRawToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteBody = await deleteResponse.ReadAsJsonAsync<JsonElement>();
        deleteBody.GetProperty("deleted").GetBoolean().Should().BeTrue();

        // Verify the command no longer appears in the list
        var listResponse = await BotGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken);

        var listBody = await listResponse.ReadAsJsonAsync<JsonElement>();
        var remainingNames = listBody.EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToList();

        remainingNames.Should().NotContain(commandName,
            "deleted command should not appear in listing");
    }

    [Fact]
    public async Task DeleteCommand_AsRegularUser_Returns403()
    {
        var ctx = await SetupBotContextAsync();
        var commandName = $"delu_{Guid.NewGuid():N}"[..16];

        // Register a command
        var registerResponse = await BotPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands",
            ctx.BotRawToken,
            new { name = commandName, description = "Protected command" });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var registerBody = await registerResponse.ReadAsJsonAsync<JsonElement>();
        var commandId = registerBody.GetProperty("id").ReadLong();

        // Regular user tries to delete
        var deleteResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/commands/{commandId}",
            ctx.Owner.AccessToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Interaction Callback ────────────

    [Fact]
    public async Task InteractionCallback_AuthenticatedBot_MissingToken_Returns404()
    {
        // The interaction callback endpoint requires a valid Redis-backed interaction token.
        // Seeding a real token requires triggering the outbox pipeline (disabled in tests).
        // This test proves: (1) an authenticated bot reaches the endpoint, and (2) a missing
        // or expired interaction token produces a clean 404 (not a 500 or auth failure).
        var ctx = await SetupBotContextAsync();

        // POST to the callback endpoint with a token that does not exist in Redis
        var response = await BotPostAsync(
            "/api/v1/interactions/nonexistent-token/callback",
            ctx.BotRawToken,
            new { type = 2, content = (string?)null }); // DeferredChannelMessage = 2

        // 404 means: bot is authenticated, endpoint reached, token not found in Redis
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "an authenticated bot posting to a non-existent interaction token should get 404");
    }

    [Fact]
    public async Task InteractionCallback_UnauthenticatedBot_Returns401()
    {
        var response = await _fixture.Client.PostJsonAsync(
            "/api/v1/interactions/any-token/callback",
            new { type = 1, content = "hello" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InteractionCallback_InvalidBotToken_Returns401()
    {
        var request = BotRequest(HttpMethod.Post, "/api/v1/interactions/any-token/callback", "bad-token");
        request.Content = System.Net.Http.Json.JsonContent.Create(new { type = 1, content = "hello" });

        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InteractionCallback_AsRegularUser_Returns403()
    {
        var ctx = await SetupBotContextAsync();

        // Regular user JWT cannot reach the Bot-auth-required callback endpoint
        var response = await _helper.AuthPostAsync(
            "/api/v1/interactions/any-token/callback",
            ctx.Owner.AccessToken,
            new { type = 1, content = "hello" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
