using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class WebhookTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public WebhookTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helper Methods ────────────

    /// <summary>
    /// Creates a user, server, and channel. Returns everything needed to test webhooks.
    /// </summary>
    private async Task<WebhookTestContext> SetupAsync()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        return new WebhookTestContext(owner, serverId, channelId, conversationId);
    }

    private sealed record WebhookTestContext(
        AuthenticatedUser Owner,
        long ServerId,
        long ChannelId,
        long ConversationId);

    // ──────────── Create Incoming Webhook ────────────

    [Fact]
    public async Task CreateWebhook_AsOwner_ReturnsWebhookWithToken()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "Test Webhook" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("channelId").ReadLong().Should().Be(ctx.ChannelId);
        body.GetProperty("name").GetString().Should().Be("Test Webhook");
        body.GetProperty("createdByUserId").ReadLong().Should().Be(ctx.Owner.UserId);
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        // Token should be returned and non-empty
        var token = body.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        // URL should contain webhook ID and token
        var url = body.GetProperty("url").GetString();
        url.Should().NotBeNullOrWhiteSpace();
        url.Should().Contain("/api/v1/webhooks/");
        url.Should().Contain(token!);
    }

    [Fact]
    public async Task CreateWebhook_WithAvatar_ReturnsWebhookWithAvatar()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "Avatar Webhook", avatarUrl = "https://example.com/avatar.png" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Avatar Webhook");
        body.GetProperty("avatarUrl").GetString().Should().Be("https://example.com/avatar.png");
    }

    [Fact]
    public async Task CreateWebhook_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var joinResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Member attempts to create a webhook (should fail - no ManageWebhooks permission)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            member.AccessToken,
            new { channelId = ctx.ChannelId, name = "Forbidden Webhook" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateWebhook_Unauthenticated_Returns401()
    {
        var ctx = await SetupAsync();

        var response = await _fixture.Client.PostJsonAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            new { channelId = ctx.ChannelId, name = "Unauthenticated Webhook" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Execute Incoming Webhook (Post Message) ────────────

    [Fact]
    public async Task ExecuteWebhook_ValidToken_ReturnsCreatedMessage()
    {
        var ctx = await SetupAsync();

        // Create the webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "Message Webhook" });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();
        var token = createBody.GetProperty("token").GetString()!;

        // Execute the webhook (anonymous endpoint - no auth required)
        var executeResponse = await _fixture.Client.PostJsonAsync(
            $"/api/v1/webhooks/{webhookId}/{token}",
            new { content = "Hello from webhook!" });

        executeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var executeBody = await executeResponse.ReadAsJsonAsync<JsonElement>();
        executeBody.GetProperty("messageId").ReadLong().Should().BeGreaterThan(0);
        executeBody.GetProperty("conversationId").ReadLong().Should().Be(ctx.ConversationId);
        executeBody.GetProperty("content").GetString().Should().Be("Hello from webhook!");
        executeBody.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExecuteWebhook_WithUsernameOverride_ReturnsMessage()
    {
        var ctx = await SetupAsync();

        // Create the webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "Override Webhook" });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();
        var token = createBody.GetProperty("token").GetString()!;

        // Execute with username and avatar override
        var executeResponse = await _fixture.Client.PostJsonAsync(
            $"/api/v1/webhooks/{webhookId}/{token}",
            new
            {
                content = "Message with overrides",
                username = "Custom Bot",
                avatarUrl = "https://example.com/bot.png"
            });

        executeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var executeBody = await executeResponse.ReadAsJsonAsync<JsonElement>();
        executeBody.GetProperty("content").GetString().Should().Be("Message with overrides");

        // Verify the message was created with the correct content (username/avatarUrl
        // overrides are stored in message metadata, not the top-level response)
        executeBody.GetProperty("messageId").ValueKind.Should().NotBe(JsonValueKind.Null,
            "webhook execution should return the created message ID");
    }

    [Fact]
    public async Task ExecuteWebhook_InvalidToken_Returns403()
    {
        var ctx = await SetupAsync();

        // Create the webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "Token Test Webhook" });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();

        // Execute with wrong token
        var executeResponse = await _fixture.Client.PostJsonAsync(
            $"/api/v1/webhooks/{webhookId}/totally-wrong-token",
            new { content = "Should not work" });

        executeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExecuteWebhook_NonExistentWebhook_Returns404()
    {
        var executeResponse = await _fixture.Client.PostJsonAsync(
            "/api/v1/webhooks/999999999999/fake-token",
            new { content = "Should not work" });

        executeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExecuteWebhook_EmptyContent_Returns400()
    {
        var ctx = await SetupAsync();

        // Create the webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "Validation Webhook" });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();
        var token = createBody.GetProperty("token").GetString()!;

        // Execute with empty content
        var executeResponse = await _fixture.Client.PostJsonAsync(
            $"/api/v1/webhooks/{webhookId}/{token}",
            new { content = "" });

        executeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── List Incoming Webhooks ────────────

    [Fact]
    public async Task ListWebhooks_AsOwner_ReturnsCreatedWebhooks()
    {
        var ctx = await SetupAsync();

        // Create two webhooks
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "Webhook One" });

        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "Webhook Two" });

        // List webhooks
        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var webhooks = await response.ReadAsJsonAsync<JsonElement>();
        var list = webhooks.EnumerateArray().ToList();
        list.Should().HaveCountGreaterThanOrEqualTo(2);

        // Verify all returned webhooks have the expected fields
        foreach (var wh in list)
        {
            wh.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
            wh.GetProperty("channelId").ReadLong().Should().Be(ctx.ChannelId);
            wh.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
            wh.GetProperty("createdByUserId").ReadLong().Should().Be(ctx.Owner.UserId);
        }

        // Verify our named webhooks are present
        var names = list.Select(w => w.GetProperty("name").GetString()).ToList();
        names.Should().Contain("Webhook One");
        names.Should().Contain("Webhook Two");
    }

    [Fact]
    public async Task ListWebhooks_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Delete Incoming Webhook ────────────

    [Fact]
    public async Task DeleteWebhook_AsOwner_Returns204()
    {
        var ctx = await SetupAsync();

        // Create a webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "Delete Me" });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();

        // Delete it
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks/{webhookId}",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it no longer appears in the list
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken);

        var webhooks = await listResponse.ReadAsJsonAsync<JsonElement>();
        var remaining = webhooks.EnumerateArray()
            .Where(w => w.GetProperty("id").ReadLong() == webhookId)
            .ToList();

        remaining.Should().BeEmpty("deleted webhook should not appear in listing");
    }

    [Fact]
    public async Task DeleteWebhook_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Create a webhook as owner
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "Protected Webhook" });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();

        // Member attempts to delete (should fail)
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks/{webhookId}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteWebhook_NonExistent_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks/999999999999",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteWebhook_ExecuteAfterDeletion_ReturnsNotFound()
    {
        var ctx = await SetupAsync();

        // Create a webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "Ephemeral Webhook" });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();
        var token = createBody.GetProperty("token").GetString()!;

        // Delete it
        var deleteResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks/{webhookId}",
            ctx.Owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Try to execute the deleted webhook
        var executeResponse = await _fixture.Client.PostJsonAsync(
            $"/api/v1/webhooks/{webhookId}/{token}",
            new { content = "Should not work" });

        executeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Create Outgoing Webhook ────────────

    [Fact]
    public async Task CreateOutgoingWebhook_AsOwner_ReturnsWebhookWithSecret()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "https://example.com/webhook",
                eventTypes = new[] { "MessageCreated", "MemberJoined" }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("serverId").ReadLong().Should().Be(ctx.ServerId);
        body.GetProperty("targetUrl").GetString().Should().Be("https://example.com/webhook");
        body.GetProperty("isActive").GetBoolean().Should().BeTrue();
        body.GetProperty("createdByUserId").ReadLong().Should().Be(ctx.Owner.UserId);
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        // Secret should be a 64-character hex string (32 bytes)
        var secret = body.GetProperty("secret").GetString();
        secret.Should().NotBeNullOrWhiteSpace();
        secret.Should().HaveLength(64);
        secret.Should().MatchRegex("^[0-9a-f]+$");

        // Event types should be returned
        var eventTypes = body.GetProperty("eventTypes").EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        eventTypes.Should().Contain("MessageCreated");
        eventTypes.Should().Contain("MemberJoined");
    }

    [Fact]
    public async Task CreateOutgoingWebhook_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            member.AccessToken,
            new
            {
                targetUrl = "https://example.com/webhook",
                eventTypes = new[] { "MessageCreated" }
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateOutgoingWebhook_InvalidEventType_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "https://example.com/webhook",
                eventTypes = new[] { "CompletelyInvalidEvent" }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOutgoingWebhook_InvalidUrl_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "ftp://not-http.example.com/webhook",
                eventTypes = new[] { "MessageCreated" }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOutgoingWebhook_DeduplicatesEventTypes()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "https://example.com/webhook",
                eventTypes = new[] { "MessageCreated", "MessageCreated", "MemberJoined" }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var eventTypes = body.GetProperty("eventTypes").EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        eventTypes.Should().HaveCount(2);
        eventTypes.Should().Contain("MessageCreated");
        eventTypes.Should().Contain("MemberJoined");
    }

    // ──────────── SSRF Protection ────────────

    [Fact]
    public async Task CreateOutgoingWebhook_PrivateIpTarget_AcceptsAtCreation()
    {
        // SSRF protection is enforced at delivery time, not at creation time.
        // The creation endpoint validates URL format only. Private IP targets
        // are blocked by OutgoingWebhookDeliveryService when it resolves DNS.
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "http://127.0.0.1:8080/webhook",
                eventTypes = new[] { "MessageCreated" }
            });

        // Creation succeeds - SSRF is blocked at delivery time
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("targetUrl").GetString().Should().Be("http://127.0.0.1:8080/webhook");
    }

    // ──────────── Update Outgoing Webhook ────────────

    [Fact]
    public async Task UpdateOutgoingWebhook_TargetUrl_ReturnsUpdated()
    {
        var ctx = await SetupAsync();

        // Create outgoing webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "https://example.com/original",
                eventTypes = new[] { "MessageCreated" }
            });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();

        // Update the target URL
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks/{webhookId}",
            ctx.Owner.AccessToken,
            new { targetUrl = "https://example.com/updated" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(webhookId);
        body.GetProperty("targetUrl").GetString().Should().Be("https://example.com/updated");
    }

    [Fact]
    public async Task UpdateOutgoingWebhook_EventTypes_ReturnsUpdated()
    {
        var ctx = await SetupAsync();

        // Create outgoing webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "https://example.com/events",
                eventTypes = new[] { "MessageCreated" }
            });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();

        // Update event types
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks/{webhookId}",
            ctx.Owner.AccessToken,
            new { eventTypes = new[] { "MemberJoined", "MemberLeft" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var eventTypes = body.GetProperty("eventTypes").EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        eventTypes.Should().Contain("MemberJoined");
        eventTypes.Should().Contain("MemberLeft");
        eventTypes.Should().NotContain("MessageCreated");
    }

    [Fact]
    public async Task UpdateOutgoingWebhook_Deactivate_ReturnsInactive()
    {
        var ctx = await SetupAsync();

        // Create outgoing webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "https://example.com/toggle",
                eventTypes = new[] { "MessageCreated" }
            });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();
        createBody.GetProperty("isActive").GetBoolean().Should().BeTrue();

        // Deactivate
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks/{webhookId}",
            ctx.Owner.AccessToken,
            new { isActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task UpdateOutgoingWebhook_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Create outgoing webhook as owner
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "https://example.com/protected",
                eventTypes = new[] { "MessageCreated" }
            });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();

        // Member attempts to update (should fail)
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks/{webhookId}",
            member.AccessToken,
            new { targetUrl = "https://evil.com/steal" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateOutgoingWebhook_NonExistent_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks/999999999999",
            ctx.Owner.AccessToken,
            new { targetUrl = "https://example.com/nope" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── List Outgoing Webhooks ────────────

    [Fact]
    public async Task ListOutgoingWebhooks_AsOwner_ReturnsCreatedWebhooks()
    {
        var ctx = await SetupAsync();

        // Create two outgoing webhooks
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "https://example.com/first",
                eventTypes = new[] { "MessageCreated" }
            });

        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "https://example.com/second",
                eventTypes = new[] { "MemberJoined" }
            });

        // List outgoing webhooks
        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var webhooks = await response.ReadAsJsonAsync<JsonElement>();
        var list = webhooks.EnumerateArray().ToList();
        list.Should().HaveCountGreaterThanOrEqualTo(2);

        var urls = list.Select(w => w.GetProperty("targetUrl").GetString()).ToList();
        urls.Should().Contain("https://example.com/first");
        urls.Should().Contain("https://example.com/second");

        // Verify secret is NOT returned in list response
        foreach (var wh in list)
        {
            wh.TryGetProperty("secret", out _).Should().BeFalse(
                "secret should not be returned in list response (only at creation time)");
        }
    }

    [Fact]
    public async Task ListOutgoingWebhooks_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Delete Outgoing Webhook ────────────

    [Fact]
    public async Task DeleteOutgoingWebhook_AsOwner_Returns204()
    {
        var ctx = await SetupAsync();

        // Create outgoing webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "https://example.com/delete-me",
                eventTypes = new[] { "MessageCreated" }
            });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();

        // Delete it
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks/{webhookId}",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it no longer appears in the list
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken);

        var webhooks = await listResponse.ReadAsJsonAsync<JsonElement>();
        var remaining = webhooks.EnumerateArray()
            .Where(w => w.GetProperty("id").ReadLong() == webhookId)
            .ToList();

        remaining.Should().BeEmpty("deleted outgoing webhook should not appear in listing");
    }

    [Fact]
    public async Task DeleteOutgoingWebhook_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Create outgoing webhook as owner
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks",
            ctx.Owner.AccessToken,
            new
            {
                targetUrl = "https://example.com/protected",
                eventTypes = new[] { "MessageCreated" }
            });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();

        // Member attempts to delete (should fail)
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks/{webhookId}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteOutgoingWebhook_NonExistent_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/outgoing-webhooks/999999999999",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Webhook Message Appears in Channel ────────────

    [Fact]
    public async Task ExecuteWebhook_MessageAppearsInChannelHistory()
    {
        var ctx = await SetupAsync();

        // Create webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/webhooks",
            ctx.Owner.AccessToken,
            new { channelId = ctx.ChannelId, name = "History Webhook" });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();
        var token = createBody.GetProperty("token").GetString()!;

        // Execute webhook to post a message
        var executeResponse = await _fixture.Client.PostJsonAsync(
            $"/api/v1/webhooks/{webhookId}/{token}",
            new { content = "Webhook message in history" });

        executeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the message appears in the channel message history
        var historyResponse = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{ctx.ConversationId}/messages",
            ctx.Owner.AccessToken);

        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyBody = await historyResponse.ReadAsJsonAsync<JsonElement>();
        var messages = historyBody.GetProperty("messages").EnumerateArray().ToList();
        messages.Should().HaveCountGreaterThanOrEqualTo(1);

        var webhookMessage = messages.FirstOrDefault(m =>
            m.GetProperty("content").GetString() == "Webhook message in history");
        webhookMessage.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "webhook message should appear in channel history");
    }
}
