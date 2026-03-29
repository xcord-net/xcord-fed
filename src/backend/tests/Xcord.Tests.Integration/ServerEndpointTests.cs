using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class ServerEndpointTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public ServerEndpointTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Create Server ────────────

    [Fact]
    public async Task CreateServer_ValidName_ReturnsServerWithOwner()
    {
        var user = await _helper.RegisterUserAsync();

        var server = await _helper.CreateServerAsync(user.AccessToken, "My Test Server");

        server.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        server.GetProperty("name").GetString().Should().Be("My Test Server");
        server.GetProperty("ownerId").ReadLong().Should().Be(user.UserId);
        server.GetProperty("memberCount").GetInt32().Should().Be(1);
        server.GetProperty("description").ValueKind.Should().Be(JsonValueKind.Null);
        server.GetProperty("iconUrl").ValueKind.Should().Be(JsonValueKind.Null);
        server.GetProperty("bannerUrl").ValueKind.Should().Be(JsonValueKind.Null);
        server.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateServer_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.PostJsonAsync("/api/v1/servers", new
        {
            name = "Unauthorized Server"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Get Server ────────────

    [Fact]
    public async Task GetServer_AsMember_ReturnsServerDetails()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}", user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(serverId);
        body.GetProperty("ownerId").ReadLong().Should().Be(user.UserId);
        body.GetProperty("memberCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetServer_AsNonMember_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var nonMember = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}", nonMember.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Update Server ────────────

    [Fact]
    public async Task UpdateServer_AsOwner_ReturnsUpdatedServer()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken, "Original Name");
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync($"/api/v1/servers/{serverId}", user.AccessToken, new
        {
            name = "Updated Name",
            description = "Updated description",
            preferredLocale = "en-US"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(serverId);
        body.GetProperty("name").GetString().Should().Be("Updated Name");
        body.GetProperty("description").GetString().Should().Be("Updated description");
        body.GetProperty("preferredLocale").GetString().Should().Be("en-US");
        body.GetProperty("ownerId").ReadLong().Should().Be(user.UserId);
    }

    [Fact]
    public async Task UpdateServer_AsNonOwner_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Member tries to update server
        var response = await _helper.AuthPatchAsync($"/api/v1/servers/{serverId}", member.AccessToken, new
        {
            name = "Hacked Name"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Delete Server ────────────

    [Fact]
    public async Task DeleteServer_AsOwner_Returns204()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync($"/api/v1/servers/{serverId}", user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify server is soft-deleted (cannot be retrieved)
        var getResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}", user.AccessToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteServer_AsNonOwner_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Member tries to delete server
        var response = await _helper.AuthDeleteAsync($"/api/v1/servers/{serverId}", member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Leave Server ────────────

    [Fact]
    public async Task LeaveServer_AsMember_Returns204()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Member leaves
        var response = await _helper.AuthDeleteAsync($"/api/v1/servers/{serverId}/members/@me", member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify member can no longer access server
        var getResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}", member.AccessToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LeaveServer_AsOwner_Returns400()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync($"/api/v1/servers/{serverId}/members/@me", user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Create Invite ────────────

    [Fact]
    public async Task CreateInvite_AsMember_ReturnsInvite()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync($"/api/v1/servers/{serverId}/invites", user.AccessToken, new
        {
            maxUses = 10,
            expiresAt = DateTimeOffset.UtcNow.AddDays(7)
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("serverId").ReadLong().Should().Be(serverId);
        body.GetProperty("createdByUserId").ReadLong().Should().Be(user.UserId);
        body.GetProperty("maxUses").GetInt32().Should().Be(10);
        body.GetProperty("uses").GetInt32().Should().Be(0);
    }

    // ──────────── Join Server via Invite ────────────

    [Fact]
    public async Task JoinServer_ValidInvite_JoinsAndReturnsServer()
    {
        var owner = await _helper.RegisterUserAsync();
        var joiner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Create invite
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);

        // Join server
        var response = await _helper.JoinServerAsync(joiner.AccessToken, inviteCode);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(serverId);
        body.GetProperty("memberCount").GetInt32().Should().Be(2); // Owner + joiner

        // Verify joiner can now access server
        var getResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}", joiner.AccessToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task JoinServer_AlreadyMember_ReturnsServerWithoutError()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Create invite and join
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Try to join again with the same invite
        var response = await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(serverId);
        body.GetProperty("memberCount").GetInt32().Should().Be(2); // Still just owner + member (not incremented)
    }
}
