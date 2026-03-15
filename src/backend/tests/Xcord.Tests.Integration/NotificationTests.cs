using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class NotificationTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public NotificationTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helpers ────────────

    private async Task<HttpResponseMessage> AuthPutAsync(string url, string accessToken, object body)
    {
        var request = TestHelper.AuthRequest(HttpMethod.Put, url, accessToken);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        return await _fixture.Client.SendAsync(request);
    }

    // ──────────── Get Notification Settings ────────────

    [Fact]
    public async Task GetNotificationSettings_NoSettings_ReturnsEmptyList()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/users/@me/notification-settings",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("muteAll").GetBoolean().Should().BeFalse();
        body.GetProperty("settings").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task GetNotificationSettings_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.GetAsync("/api/v1/users/@me/notification-settings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetNotificationSettings_FilterByServer_ReturnsOnlyServerSettings()
    {
        var user = await _helper.RegisterUserAsync();
        var server1 = await _helper.CreateServerAsync(user.AccessToken);
        var server1Id = server1.GetProperty("id").ReadLong();
        var server2 = await _helper.CreateServerAsync(user.AccessToken);
        var server2Id = server2.GetProperty("id").ReadLong();

        // Create settings for both servers
        await AuthPutAsync(
            "/api/v1/users/@me/notification-settings",
            user.AccessToken,
            new { serverId = server1Id, channelId = (long?)null, level = "MentionsOnly" });

        await AuthPutAsync(
            "/api/v1/users/@me/notification-settings",
            user.AccessToken,
            new { serverId = server2Id, channelId = (long?)null, level = "None" });

        // Filter by server1
        var response = await _helper.AuthGetAsync(
            $"/api/v1/users/@me/notification-settings?serverId={server1Id}",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var settings = body.GetProperty("settings").EnumerateArray().ToList();
        settings.Should().HaveCount(1);
        settings[0].GetProperty("serverId").ReadLong().Should().Be(server1Id);
        settings[0].GetProperty("level").GetString().Should().Be("MentionsOnly");
    }

    // ──────────── Update Notification Setting ────────────

    [Fact]
    public async Task UpdateNotificationSetting_ServerLevel_CreatesNewSetting()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await AuthPutAsync(
            "/api/v1/users/@me/notification-settings",
            user.AccessToken,
            new
            {
                serverId,
                channelId = (long?)null,
                level = "MentionsOnly",
                suppressEveryone = true,
                suppressRoles = false,
                muteUntil = (DateTimeOffset?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("userId").ReadLong().Should().Be(user.UserId);
        body.GetProperty("serverId").ReadLong().Should().Be(serverId);
        body.GetProperty("level").GetString().Should().Be("MentionsOnly");
        body.GetProperty("suppressEveryone").GetBoolean().Should().BeTrue();
        body.GetProperty("suppressRoles").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task UpdateNotificationSetting_ChannelLevel_CreatesNewSetting()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();

        var response = await AuthPutAsync(
            "/api/v1/users/@me/notification-settings",
            user.AccessToken,
            new
            {
                serverId,
                channelId,
                level = "None",
                suppressEveryone = false,
                suppressRoles = true,
                muteUntil = (DateTimeOffset?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("channelId").ReadLong().Should().Be(channelId);
        body.GetProperty("level").GetString().Should().Be("None");
        body.GetProperty("suppressRoles").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateNotificationSetting_UpdateExisting_ReturnsUpdatedSetting()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Create initial setting
        await AuthPutAsync(
            "/api/v1/users/@me/notification-settings",
            user.AccessToken,
            new { serverId, channelId = (long?)null, level = "MentionsOnly" });

        // Update to None
        var response = await AuthPutAsync(
            "/api/v1/users/@me/notification-settings",
            user.AccessToken,
            new { serverId, channelId = (long?)null, level = "None", suppressEveryone = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("level").GetString().Should().Be("None");
        body.GetProperty("suppressEveryone").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateNotificationSetting_WithMuteUntil_PersistsMuteExpiry()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var muteUntil = DateTimeOffset.UtcNow.AddHours(2);

        var response = await AuthPutAsync(
            "/api/v1/users/@me/notification-settings",
            user.AccessToken,
            new { serverId, channelId = (long?)null, level = "None", muteUntil });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("muteUntil").GetDateTimeOffset().Should().BeCloseTo(muteUntil, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateNotificationSetting_NonExistentServer_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await AuthPutAsync(
            "/api/v1/users/@me/notification-settings",
            user.AccessToken,
            new { serverId = 999_999_999_999L, channelId = (long?)null, level = "None" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateNotificationSetting_NotAMember_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var nonMember = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await AuthPutAsync(
            "/api/v1/users/@me/notification-settings",
            nonMember.AccessToken,
            new { serverId, channelId = (long?)null, level = "None" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateNotificationSetting_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.PutJsonAsync(
            "/api/v1/users/@me/notification-settings",
            new { serverId = 1L, channelId = (long?)null, level = "None" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Delete Notification Setting ────────────

    [Fact]
    public async Task DeleteNotificationSetting_OwnSetting_RemovesSetting()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Create a setting first
        var createResponse = await AuthPutAsync(
            "/api/v1/users/@me/notification-settings",
            user.AccessToken,
            new { serverId, channelId = (long?)null, level = "MentionsOnly" });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var settingId = createBody.GetProperty("id").ReadLong();

        // Delete it
        var deleteResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/users/@me/notification-settings/{settingId}",
            user.AccessToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify it no longer appears in the list
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/users/@me/notification-settings?serverId={serverId}",
            user.AccessToken);

        var listBody = await listResponse.ReadAsJsonAsync<JsonElement>();
        listBody.GetProperty("settings").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteNotificationSetting_OtherUsersSetting_Returns403()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user1.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // User1 creates a setting
        var createResponse = await AuthPutAsync(
            "/api/v1/users/@me/notification-settings",
            user1.AccessToken,
            new { serverId, channelId = (long?)null, level = "None" });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var settingId = createBody.GetProperty("id").ReadLong();

        // User2 tries to delete user1's setting
        var deleteResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/users/@me/notification-settings/{settingId}",
            user2.AccessToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteNotificationSetting_NonExistent_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthDeleteAsync(
            "/api/v1/users/@me/notification-settings/999999999999",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteNotificationSetting_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.DeleteAsync(
            "/api/v1/users/@me/notification-settings/123456");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
