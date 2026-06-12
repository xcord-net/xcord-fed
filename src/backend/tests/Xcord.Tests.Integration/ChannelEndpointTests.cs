using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class ChannelEndpointTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public ChannelEndpointTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Create Channel ────────────

    [Fact]
    public async Task CreateChannel_AsOwner_Returns201WithChannelDetails()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            owner.AccessToken,
            new { name = "test-channel", type = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("conversationId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("serverId").ReadLong().Should().Be(serverId);
        body.GetProperty("name").GetString().Should().Be("test-channel");
        body.GetProperty("type").GetString().Should().Be("Text");
        body.GetProperty("position").GetInt32().Should().Be(0);
        body.GetProperty("isNsfw").GetBoolean().Should().BeFalse();
        body.GetProperty("requireTag").GetBoolean().Should().BeFalse();
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateChannel_WithTopic_ReturnsChannelWithTopic()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            owner.AccessToken,
            new { name = "topic-channel", type = 0, topic = "Channel for testing topics" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("topic-channel");
        body.GetProperty("topic").GetString().Should().Be("Channel for testing topics");
    }

    [Fact]
    public async Task CreateChannel_VoiceType_Returns201()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            owner.AccessToken,
            new { name = "voice-channel", type = 1 }); // ChannelType.Voice

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("voice-channel");
        body.GetProperty("type").GetString().Should().Be("Voice");
    }

    [Fact]
    public async Task CreateChannel_AsNonOwnerMember_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var joinResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Member attempts to create a channel (should fail - no ManageChannels permission)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "forbidden-channel", type = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateChannel_Unauthenticated_Returns401()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _fixture.Client.PostJsonAsync(
            $"/api/v1/servers/{serverId}/channels",
            new { name = "unauthenticated-channel", type = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Update Channel ────────────

    [Fact]
    public async Task UpdateChannel_AsOwner_ReturnsUpdatedChannel()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId, "original-name");
        var channelId = channel.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}",
            owner.AccessToken,
            new { name = "updated-name", topic = "Updated topic" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(channelId);
        body.GetProperty("name").GetString().Should().Be("updated-name");
        body.GetProperty("topic").GetString().Should().Be("Updated topic");
    }

    [Fact]
    public async Task UpdateChannel_ChangeName_ReturnsNewName()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId, "old-channel");
        var channelId = channel.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}",
            owner.AccessToken,
            new { name = "new-channel" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("new-channel");
    }

    [Fact]
    public async Task UpdateChannel_AsNonOwner_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId, "test-channel");
        var channelId = channel.GetProperty("id").ReadLong();

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var joinResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Member attempts to update the channel (should fail)
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}",
            member.AccessToken,
            new { name = "forbidden-update" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Delete Channel ────────────

    [Fact]
    public async Task DeleteChannel_AsOwner_Returns204()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId, "channel-to-delete");
        var channelId = channel.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{channelId}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteChannel_AsNonOwner_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId, "protected-channel");
        var channelId = channel.GetProperty("id").ReadLong();

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var joinResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Member attempts to delete the channel (should fail)
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{channelId}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListChannels_MemberWithMixedOverrides_SeesOnlyViewableChannels()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var visible = await _helper.CreateChannelAsync(owner.AccessToken, serverId, name: "visible-channel");
        var hidden = await _helper.CreateChannelAsync(owner.AccessToken, serverId, name: "hidden-channel");
        var allowed = await _helper.CreateChannelAsync(owner.AccessToken, serverId, name: "user-allowed-channel");
        var hiddenId = hidden.GetProperty("id").ReadLong();
        var allowedId = allowed.GetProperty("id").ReadLong();

        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var member = await _helper.RegisterUserAsync();
        (await _helper.JoinServerAsync(member.AccessToken, inviteCode)).EnsureSuccessStatusCode();

        long everyoneGroupId;
        await using (var db = _fixture.CreateDbContext())
        {
            everyoneGroupId = await db.Groups
                .AsNoTracking()
                .Where(g => g.ServerId == serverId && g.IsEveryone)
                .Select(g => g.Id)
                .FirstAsync();
        }

        async Task PutOverride(long channelId, string subjectId, string verdict)
        {
            var request = TestHelper.AuthRequest(
                HttpMethod.Put, $"/api/v1/servers/{serverId}/channels/{channelId}/permissions", owner.AccessToken);
            request.Content = JsonContent.Create(new
            {
                subjectId,
                permissions = new Dictionary<string, string> { ["ViewChannel"] = verdict }
            });
            var response = await _fixture.Client.SendAsync(request);
            response.IsSuccessStatusCode.Should().BeTrue(
                $"override update should succeed: {await response.Content.ReadAsStringAsync()}");
        }

        // Hide two channels from @everyone, then re-allow one specifically for the member.
        await PutOverride(hiddenId, everyoneGroupId.ToString(), "Deny");
        await PutOverride(allowedId, everyoneGroupId.ToString(), "Deny");
        await PutOverride(allowedId, member.UserId.ToString(), "Allow");

        var listResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/channels", member.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.ReadAsJsonAsync<JsonElement>();
        var names = body.GetProperty("channels").EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToList();

        names.Should().Contain("visible-channel");
        names.Should().Contain("user-allowed-channel", "a user-level Allow override beats the @everyone Deny");
        names.Should().NotContain("hidden-channel", "the @everyone Deny override hides the channel from regular members");

        // The owner (admin) still sees everything.
        var ownerList = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/channels", owner.AccessToken);
        var ownerBody = await ownerList.ReadAsJsonAsync<JsonElement>();
        ownerBody.GetProperty("channels").EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .Should().Contain(["visible-channel", "hidden-channel", "user-allowed-channel"]);
    }
}
