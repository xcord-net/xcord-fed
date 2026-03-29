using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class AdminBotTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public AdminBotTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helpers ────────────

    /// <summary>
    /// Registers a user, promotes them to admin via DB, and re-logs in to get
    /// a JWT with the admin claim.
    /// </summary>
    private async Task<AuthenticatedUser> RegisterAdminAsync()
    {
        var user = await _helper.RegisterUserAsync();

        // Promote to admin via direct DB update
        await using (var db = _fixture.CreateDbContext())
        {
            var dbUser = await db.Users.FirstAsync(u => u.Id == user.UserId);
            dbUser.IsAdmin = true;
            await db.SaveChangesAsync();
        }

        // Re-login to get a fresh token with admin=true claim
        var loginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email = user.Email,
            password = user.Password
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "admin re-login should succeed");

        var adminToken = TestHelper.ExtractAccessTokenFromCookies(loginResponse);

        return new AuthenticatedUser
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            Password = user.Password,
            AccessToken = adminToken
        };
    }

    /// <summary>
    /// Creates a bot via the admin endpoint and returns the response body.
    /// </summary>
    private async Task<JsonElement> CreateBotAsync(string adminToken, string? username = null)
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
                tokenName = $"default-token-{id}",
                roles = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"bot creation should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    // ──────────── Create Bot ────────────

    [Fact]
    public async Task CreateBot_AsAdmin_Returns201WithBotAndToken()
    {
        var admin = await RegisterAdminAsync();

        var body = await CreateBotAsync(admin.AccessToken);

        body.GetProperty("userId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("username").GetString().Should().StartWith("bot_");
        body.GetProperty("displayName").GetString().Should().Contain("Test Bot");
        body.GetProperty("tokenId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("tokenName").GetString().Should().StartWith("default-token-");
        body.GetProperty("rawToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("roles").ReadLong().Should().Be(0);
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateBot_AsNonAdmin_Returns403()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/admin/bots",
            user.AccessToken,
            new
            {
                username = "forbidden_bot",
                displayName = "Forbidden Bot",
                tokenName = "default",
                roles = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateBot_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.PostJsonAsync(
            "/api/v1/admin/bots",
            new
            {
                username = "unauth_bot",
                displayName = "Unauth Bot",
                tokenName = "default",
                roles = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateBot_EmptyUsername_Returns400()
    {
        var admin = await RegisterAdminAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/admin/bots",
            admin.AccessToken,
            new
            {
                username = "",
                displayName = "No Name Bot",
                tokenName = "default",
                roles = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBot_InvalidUsernameChars_Returns400()
    {
        var admin = await RegisterAdminAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/admin/bots",
            admin.AccessToken,
            new
            {
                username = "bot with spaces!",
                displayName = "Bad Name Bot",
                tokenName = "default",
                roles = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBot_DuplicateUsername_Returns409()
    {
        var admin = await RegisterAdminAsync();
        var uniqueName = $"bot_dup_{Guid.NewGuid():N}"[..20];

        // Create first bot
        await CreateBotAsync(admin.AccessToken, uniqueName);

        // Try to create another with the same username
        var response = await _helper.AuthPostAsync(
            "/api/v1/admin/bots",
            admin.AccessToken,
            new
            {
                username = uniqueName,
                displayName = "Duplicate Bot",
                tokenName = "default",
                roles = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateBot_EmptyDisplayName_Returns400()
    {
        var admin = await RegisterAdminAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/admin/bots",
            admin.AccessToken,
            new
            {
                username = $"bot_{Guid.NewGuid():N}"[..12],
                displayName = "",
                tokenName = "default",
                roles = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBot_EmptyTokenName_Returns400()
    {
        var admin = await RegisterAdminAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/admin/bots",
            admin.AccessToken,
            new
            {
                username = $"bot_{Guid.NewGuid():N}"[..12],
                displayName = "Good Bot",
                tokenName = "",
                roles = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── List Bots ────────────

    [Fact]
    public async Task ListBots_AsAdmin_ReturnsCreatedBots()
    {
        var admin = await RegisterAdminAsync();

        // Create two bots
        var bot1 = await CreateBotAsync(admin.AccessToken);
        var bot2 = await CreateBotAsync(admin.AccessToken);

        var bot1UserId = bot1.GetProperty("userId").ReadLong();
        var bot2UserId = bot2.GetProperty("userId").ReadLong();

        // List bots
        var response = await _helper.AuthGetAsync(
            "/api/v1/admin/bots",
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var bots = body.GetProperty("bots").EnumerateArray().ToList();
        bots.Should().HaveCountGreaterThanOrEqualTo(2);

        var userIds = bots.Select(b => b.GetProperty("userId").ReadLong()).ToList();
        userIds.Should().Contain(bot1UserId);
        userIds.Should().Contain(bot2UserId);
    }

    [Fact]
    public async Task ListBots_AsNonAdmin_Returns403()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/admin/bots",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListBots_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.GetAsync("/api/v1/admin/bots");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Create Bot Token ────────────

    [Fact]
    public async Task CreateBotToken_AsAdmin_ReturnsNewToken()
    {
        var admin = await RegisterAdminAsync();
        var bot = await CreateBotAsync(admin.AccessToken);
        var botUserId = bot.GetProperty("userId").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/admin/bots/{botUserId}/tokens",
            admin.AccessToken,
            new
            {
                tokenName = "second-token",
                roles = 42L
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("tokenId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("tokenName").GetString().Should().Be("second-token");
        body.GetProperty("rawToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("roles").ReadLong().Should().Be(42);
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateBotToken_ForNonExistentBot_Returns404()
    {
        var admin = await RegisterAdminAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/admin/bots/999999999999/tokens",
            admin.AccessToken,
            new
            {
                tokenName = "orphan-token",
                roles = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateBotToken_ForNonBotUser_Returns400()
    {
        var admin = await RegisterAdminAsync();

        // Use the admin's own user ID (not a bot)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/admin/bots/{admin.UserId}/tokens",
            admin.AccessToken,
            new
            {
                tokenName = "bad-token",
                roles = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBotToken_AsNonAdmin_Returns403()
    {
        var admin = await RegisterAdminAsync();
        var user = await _helper.RegisterUserAsync();
        var bot = await CreateBotAsync(admin.AccessToken);
        var botUserId = bot.GetProperty("userId").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/admin/bots/{botUserId}/tokens",
            user.AccessToken,
            new
            {
                tokenName = "forbidden-token",
                roles = 0L
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── List Bot Tokens ────────────

    [Fact]
    public async Task ListBotTokens_AsAdmin_ReturnsTokensForBot()
    {
        var admin = await RegisterAdminAsync();
        var bot = await CreateBotAsync(admin.AccessToken);
        var botUserId = bot.GetProperty("userId").ReadLong();

        // Create a second token
        await _helper.AuthPostAsync(
            $"/api/v1/admin/bots/{botUserId}/tokens",
            admin.AccessToken,
            new { tokenName = "extra-token", roles = 0L });

        // List tokens
        var response = await _helper.AuthGetAsync(
            $"/api/v1/admin/bots/{botUserId}/tokens",
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var tokens = body.GetProperty("tokens").EnumerateArray().ToList();
        tokens.Should().HaveCountGreaterThanOrEqualTo(2);

        // Tokens should have metadata but NOT the raw token
        foreach (var token in tokens)
        {
            token.GetProperty("tokenId").ReadLong().Should().BeGreaterThan(0);
            token.GetProperty("tokenName").GetString().Should().NotBeNullOrWhiteSpace();
            token.GetProperty("isRevoked").GetBoolean().Should().BeFalse();
            token.TryGetProperty("rawToken", out _).Should().BeFalse(
                "raw token should never be returned after creation");
        }
    }

    [Fact]
    public async Task ListBotTokens_ForNonExistentBot_Returns404()
    {
        var admin = await RegisterAdminAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/admin/bots/999999999999/tokens",
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListBotTokens_AsNonAdmin_Returns403()
    {
        var admin = await RegisterAdminAsync();
        var user = await _helper.RegisterUserAsync();
        var bot = await CreateBotAsync(admin.AccessToken);
        var botUserId = bot.GetProperty("userId").ReadLong();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/admin/bots/{botUserId}/tokens",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Revoke Bot Token ────────────

    [Fact]
    public async Task RevokeBotToken_AsAdmin_RevokesSuccessfully()
    {
        var admin = await RegisterAdminAsync();
        var bot = await CreateBotAsync(admin.AccessToken);
        var botUserId = bot.GetProperty("userId").ReadLong();
        var tokenId = bot.GetProperty("tokenId").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/admin/bots/{botUserId}/tokens/{tokenId}",
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("tokenId").ReadLong().Should().Be(tokenId);
        body.GetProperty("success").GetBoolean().Should().BeTrue();

        // Verify the token shows as revoked in the list
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/admin/bots/{botUserId}/tokens",
            admin.AccessToken);

        var listBody = await listResponse.ReadAsJsonAsync<JsonElement>();
        var tokens = listBody.GetProperty("tokens").EnumerateArray().ToList();
        var revokedToken = tokens.FirstOrDefault(t => t.GetProperty("tokenId").ReadLong() == tokenId);
        revokedToken.GetProperty("isRevoked").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task RevokeBotToken_AlreadyRevoked_Returns409()
    {
        var admin = await RegisterAdminAsync();
        var bot = await CreateBotAsync(admin.AccessToken);
        var botUserId = bot.GetProperty("userId").ReadLong();
        var tokenId = bot.GetProperty("tokenId").ReadLong();

        // Revoke once
        await _helper.AuthDeleteAsync(
            $"/api/v1/admin/bots/{botUserId}/tokens/{tokenId}",
            admin.AccessToken);

        // Try to revoke again
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/admin/bots/{botUserId}/tokens/{tokenId}",
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RevokeBotToken_NonExistentToken_Returns404()
    {
        var admin = await RegisterAdminAsync();
        var bot = await CreateBotAsync(admin.AccessToken);
        var botUserId = bot.GetProperty("userId").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/admin/bots/{botUserId}/tokens/999999999999",
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeBotToken_AsNonAdmin_Returns403()
    {
        var admin = await RegisterAdminAsync();
        var user = await _helper.RegisterUserAsync();
        var bot = await CreateBotAsync(admin.AccessToken);
        var botUserId = bot.GetProperty("userId").ReadLong();
        var tokenId = bot.GetProperty("tokenId").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/admin/bots/{botUserId}/tokens/{tokenId}",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Delete Bot ────────────

    [Fact]
    public async Task DeleteBot_AsAdmin_SoftDeletesBotAndRevokesTokens()
    {
        var admin = await RegisterAdminAsync();
        var bot = await CreateBotAsync(admin.AccessToken);
        var botUserId = bot.GetProperty("userId").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/admin/bots/{botUserId}",
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("botId").ReadLong().Should().Be(botUserId);
        body.GetProperty("success").GetBoolean().Should().BeTrue();

        // Verify the bot user is soft-deleted in the DB
        await using var db = _fixture.CreateDbContext();
        var dbUser = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == botUserId);
        dbUser.Should().NotBeNull();
        dbUser!.DeletedAt.Should().NotBeNull("bot should be soft-deleted");

        // Verify all tokens are revoked
        var tokens = await db.BotTokens
            .Where(bt => bt.UserId == botUserId)
            .ToListAsync();
        tokens.Should().AllSatisfy(t => t.IsRevoked.Should().BeTrue());
    }

    [Fact]
    public async Task DeleteBot_NonExistentBot_Returns404()
    {
        var admin = await RegisterAdminAsync();

        var response = await _helper.AuthDeleteAsync(
            "/api/v1/admin/bots/999999999999",
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteBot_NotABot_Returns400()
    {
        var admin = await RegisterAdminAsync();

        // Try to delete the admin user (who is not a bot)
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/admin/bots/{admin.UserId}",
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteBot_AsNonAdmin_Returns403()
    {
        var admin = await RegisterAdminAsync();
        var user = await _helper.RegisterUserAsync();
        var bot = await CreateBotAsync(admin.AccessToken);
        var botUserId = bot.GetProperty("userId").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/admin/bots/{botUserId}",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteBot_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.DeleteAsync("/api/v1/admin/bots/123456");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
