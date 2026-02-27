using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class FederationTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public FederationTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helpers ────────────

    /// <summary>
    /// Generates a unique remote instance URL to avoid cross-test collisions in the shared DB.
    /// </summary>
    private static string UniqueRemoteUrl() =>
        $"https://{Guid.NewGuid():N}.remote.example.com";

    /// <summary>
    /// Generates a unique remote channel ID to avoid cross-test collisions.
    /// </summary>
    private static string UniqueRemoteChannelId() =>
        Guid.NewGuid().ToString("N")[..12];

    /// <summary>
    /// Creates a server and channel, returning (serverId, channelId, conversationId).
    /// </summary>
    private async Task<(long ServerId, long ChannelId, long ConversationId)> CreateServerAndChannelAsync(
        string accessToken)
    {
        var server = await _helper.CreateServerAsync(accessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(accessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();
        var conversationId = channel.GetProperty("conversationId").ReadLong();
        return (serverId, channelId, conversationId);
    }

    /// <summary>
    /// Creates a federation follow and returns (followId, remoteUrl, remoteChannelId).
    /// </summary>
    private async Task<(long FollowId, string RemoteUrl, string RemoteChannelId)> CreateFollowAsync(
        string accessToken, long localChannelId,
        string? remoteUrl = null,
        string? remoteChannelId = null,
        string? remoteChannelName = "remote-general")
    {
        remoteUrl ??= UniqueRemoteUrl();
        remoteChannelId ??= UniqueRemoteChannelId();

        var response = await _helper.AuthPostAsync(
            "/api/v1/federation/follows",
            accessToken,
            new
            {
                remoteInstanceUrl = remoteUrl,
                remoteChannelId,
                localChannelId,
                remoteChannelName
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        return (body.GetProperty("id").ReadLong(), remoteUrl, remoteChannelId);
    }

    // ──────────── Create Federation Follow ────────────

    [Fact]
    public async Task CreateFollow_AsOwner_Returns201WithDetails()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var remoteUrl = UniqueRemoteUrl();
        var remoteChannelId = UniqueRemoteChannelId();

        var response = await _helper.AuthPostAsync(
            "/api/v1/federation/follows",
            owner.AccessToken,
            new
            {
                remoteInstanceUrl = remoteUrl,
                remoteChannelId,
                localChannelId = channelId,
                remoteChannelName = "remote-general"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("remoteInstanceUrl").GetString().Should().Be(remoteUrl);
        body.GetProperty("localChannelId").ReadLong().Should().Be(channelId);
        body.GetProperty("remoteChannelId").GetString().Should().Be(remoteChannelId);
        body.GetProperty("remoteChannelName").GetString().Should().Be("remote-general");
        body.GetProperty("followedByUserId").ReadLong().Should().Be(owner.UserId);
        body.GetProperty("isActive").GetBoolean().Should().BeTrue();
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateFollow_DuplicateFollow_Returns409()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var remoteUrl = UniqueRemoteUrl();
        var remoteChannelId = UniqueRemoteChannelId();

        // Create first follow
        await CreateFollowAsync(owner.AccessToken, channelId,
            remoteUrl: remoteUrl, remoteChannelId: remoteChannelId);

        // Attempt duplicate follow with the same remote instance + channel
        var response = await _helper.AuthPostAsync(
            "/api/v1/federation/follows",
            owner.AccessToken,
            new
            {
                remoteInstanceUrl = remoteUrl,
                remoteChannelId,
                localChannelId = channelId,
                remoteChannelName = "remote-general"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateFollow_NonExistentLocalChannel_Returns404()
    {
        var owner = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/federation/follows",
            owner.AccessToken,
            new
            {
                remoteInstanceUrl = UniqueRemoteUrl(),
                remoteChannelId = UniqueRemoteChannelId(),
                localChannelId = 999999999999L,
                remoteChannelName = "remote-general"
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateFollow_AsNonOwnerMember_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var (serverId, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Member tries to create a federation follow (no ManageChannels permission)
        var response = await _helper.AuthPostAsync(
            "/api/v1/federation/follows",
            member.AccessToken,
            new
            {
                remoteInstanceUrl = UniqueRemoteUrl(),
                remoteChannelId = UniqueRemoteChannelId(),
                localChannelId = channelId,
                remoteChannelName = "remote-general"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateFollow_InvalidUrl_Returns400()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var response = await _helper.AuthPostAsync(
            "/api/v1/federation/follows",
            owner.AccessToken,
            new
            {
                remoteInstanceUrl = "not-a-valid-url",
                remoteChannelId = UniqueRemoteChannelId(),
                localChannelId = channelId,
                remoteChannelName = "remote-general"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateFollow_EmptyRemoteChannelId_Returns400()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var response = await _helper.AuthPostAsync(
            "/api/v1/federation/follows",
            owner.AccessToken,
            new
            {
                remoteInstanceUrl = UniqueRemoteUrl(),
                remoteChannelId = "",
                localChannelId = channelId,
                remoteChannelName = "remote-general"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── List Federation Follows ────────────

    [Fact]
    public async Task ListFollows_ReturnsActiveFollows()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var (followId, _, _) = await CreateFollowAsync(owner.AccessToken, channelId);

        var response = await _helper.AuthGetAsync(
            $"/api/v1/federation/follows?channelId={channelId}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var follows = body.GetProperty("follows").EnumerateArray().ToList();
        follows.Should().HaveCountGreaterThanOrEqualTo(1);
        follows.Should().Contain(f => f.GetProperty("id").ReadLong() == followId);
    }

    [Fact]
    public async Task ListFollows_FilterByChannel_ReturnsOnlyChannelFollows()
    {
        var owner = await _helper.RegisterUserAsync();
        var (serverId, channelId1, _) = await CreateServerAndChannelAsync(owner.AccessToken);
        var channel2 = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId2 = channel2.GetProperty("id").ReadLong();

        // Create follows on different channels with unique remote URLs
        await CreateFollowAsync(owner.AccessToken, channelId1);
        var (followId2, _, _) = await CreateFollowAsync(owner.AccessToken, channelId2);

        // Filter by channel 2
        var response = await _helper.AuthGetAsync(
            $"/api/v1/federation/follows?channelId={channelId2}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var follows = body.GetProperty("follows").EnumerateArray().ToList();
        follows.Should().HaveCount(1);
        follows[0].GetProperty("id").ReadLong().Should().Be(followId2);
    }

    // ──────────── Federation Inbox ────────────

    [Fact]
    public async Task Inbox_WithActiveFollow_AcceptsMessages()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var (_, remoteUrl, remoteChannelId) = await CreateFollowAsync(owner.AccessToken, channelId);

        // Post to federation inbox (unauthenticated -- signature verification is disabled)
        var inboxResponse = await _fixture.Client.PostJsonAsync(
            "/api/v1/federation/inbox",
            new
            {
                sourceInstanceUrl = remoteUrl,
                sourceChannelId = remoteChannelId,
                messages = new[]
                {
                    new
                    {
                        remoteMessageId = Guid.NewGuid().ToString(),
                        authorName = "RemoteUser",
                        authorAvatarUrl = (string?)null,
                        content = "Hello from remote!",
                        metadata = (string?)null,
                        createdAt = DateTimeOffset.UtcNow
                    }
                }
            });

        inboxResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await inboxResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("accepted").GetInt32().Should().Be(1);
        body.GetProperty("rejected").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Inbox_DeduplicatesAlreadyImportedMessages()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var (_, remoteUrl, remoteChannelId) = await CreateFollowAsync(owner.AccessToken, channelId);

        var remoteMessageId = Guid.NewGuid().ToString();
        var inboxPayload = new
        {
            sourceInstanceUrl = remoteUrl,
            sourceChannelId = remoteChannelId,
            messages = new[]
            {
                new
                {
                    remoteMessageId,
                    authorName = "RemoteUser",
                    authorAvatarUrl = (string?)null,
                    content = "Dedup test message",
                    metadata = (string?)null,
                    createdAt = DateTimeOffset.UtcNow
                }
            }
        };

        // First delivery
        var response1 = await _fixture.Client.PostJsonAsync("/api/v1/federation/inbox", inboxPayload);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        var body1 = await response1.ReadAsJsonAsync<JsonElement>();
        body1.GetProperty("accepted").GetInt32().Should().Be(1);

        // Second delivery with same remoteMessageId -- should be deduplicated (still "accepted" count 1,
        // but no new local messages are created since the existing one is skipped)
        var response2 = await _fixture.Client.PostJsonAsync("/api/v1/federation/inbox", inboxPayload);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        var body2 = await response2.ReadAsJsonAsync<JsonElement>();
        body2.GetProperty("accepted").GetInt32().Should().Be(1);
        body2.GetProperty("rejected").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Inbox_NoActiveFollows_Returns404()
    {
        // Post to inbox for a remote channel that nobody follows
        var response = await _fixture.Client.PostJsonAsync(
            "/api/v1/federation/inbox",
            new
            {
                sourceInstanceUrl = UniqueRemoteUrl(),
                sourceChannelId = UniqueRemoteChannelId(),
                messages = new[]
                {
                    new
                    {
                        remoteMessageId = Guid.NewGuid().ToString(),
                        authorName = "RemoteUser",
                        authorAvatarUrl = (string?)null,
                        content = "Should be rejected",
                        metadata = (string?)null,
                        createdAt = DateTimeOffset.UtcNow
                    }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Inbox_EmptyMessages_Returns400()
    {
        var response = await _fixture.Client.PostJsonAsync(
            "/api/v1/federation/inbox",
            new
            {
                sourceInstanceUrl = UniqueRemoteUrl(),
                sourceChannelId = UniqueRemoteChannelId(),
                messages = Array.Empty<object>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Inbox_MultipleMessages_AcceptsAll()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var (_, remoteUrl, remoteChannelId) = await CreateFollowAsync(owner.AccessToken, channelId);

        var messages = Enumerable.Range(1, 5).Select(i => new
        {
            remoteMessageId = Guid.NewGuid().ToString(),
            authorName = $"User{i}",
            authorAvatarUrl = (string?)null,
            content = $"Message {i} from remote",
            metadata = (string?)null,
            createdAt = DateTimeOffset.UtcNow
        }).ToArray();

        var response = await _fixture.Client.PostJsonAsync(
            "/api/v1/federation/inbox",
            new
            {
                sourceInstanceUrl = remoteUrl,
                sourceChannelId = remoteChannelId,
                messages
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("accepted").GetInt32().Should().Be(5);
        body.GetProperty("rejected").GetInt32().Should().Be(0);
    }

    // ──────────── Unfollow ────────────

    [Fact]
    public async Task Unfollow_OwnFollow_Returns200()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var (followId, _, _) = await CreateFollowAsync(owner.AccessToken, channelId);

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/federation/follows/{followId}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("deleted").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Unfollow_DeactivatesFollow_NoLongerInList()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var (followId, _, _) = await CreateFollowAsync(owner.AccessToken, channelId);

        // Unfollow
        var unfollowResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/federation/follows/{followId}",
            owner.AccessToken);
        unfollowResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify no longer in list (soft-deleted + IsActive = false)
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/federation/follows?channelId={channelId}",
            owner.AccessToken);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResponse.ReadAsJsonAsync<JsonElement>();
        var follows = body.GetProperty("follows").EnumerateArray().ToList();
        follows.Should().NotContain(f => f.GetProperty("id").ReadLong() == followId);
    }

    [Fact]
    public async Task Unfollow_NonExistentFollow_Returns404()
    {
        var owner = await _helper.RegisterUserAsync();

        var response = await _helper.AuthDeleteAsync(
            "/api/v1/federation/follows/999999999999",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unfollow_OtherUsersFollow_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var otherUser = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var (followId, _, _) = await CreateFollowAsync(owner.AccessToken, channelId);

        // Other user tries to unfollow
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/federation/follows/{followId}",
            otherUser.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Inbox_AfterUnfollow_Returns404()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId, _) = await CreateServerAndChannelAsync(owner.AccessToken);

        var (followId, remoteUrl, remoteChannelId) = await CreateFollowAsync(owner.AccessToken, channelId);

        // Unfollow
        var unfollowResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/federation/follows/{followId}",
            owner.AccessToken);
        unfollowResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Now inbox delivery should fail -- no active follows for this unique remote URL
        var inboxResponse = await _fixture.Client.PostJsonAsync(
            "/api/v1/federation/inbox",
            new
            {
                sourceInstanceUrl = remoteUrl,
                sourceChannelId = remoteChannelId,
                messages = new[]
                {
                    new
                    {
                        remoteMessageId = Guid.NewGuid().ToString(),
                        authorName = "RemoteUser",
                        authorAvatarUrl = (string?)null,
                        content = "Should be rejected after unfollow",
                        metadata = (string?)null,
                        createdAt = DateTimeOffset.UtcNow
                    }
                }
            });

        inboxResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
