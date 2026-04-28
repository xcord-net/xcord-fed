using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class BlockTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public BlockTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Block User ────────────

    [Fact]
    public async Task BlockUser_ById_Returns200()
    {
        var blocker = await _helper.RegisterUserAsync();
        var blocked = await _helper.RegisterUserAsync();

        var request = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/blocks/{blocked.UserId}",
            blocker.AccessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("blockerId").ReadLong().Should().Be(blocker.UserId);
        body.GetProperty("blockedId").ReadLong().Should().Be(blocked.UserId);
        body.GetProperty("blockedUsername").GetString().Should().Be(blocked.Username);
    }

    [Fact]
    public async Task BlockUser_ByUsername_Returns200()
    {
        var blocker = await _helper.RegisterUserAsync();
        var blocked = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/users/@me/blocks",
            blocker.AccessToken,
            new { username = blocked.Username });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("blockerId").ReadLong().Should().Be(blocker.UserId);
        body.GetProperty("blockedId").ReadLong().Should().Be(blocked.UserId);
    }

    [Fact]
    public async Task BlockUser_Self_Returns400()
    {
        var user = await _helper.RegisterUserAsync();

        var request = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/blocks/{user.UserId}",
            user.AccessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BlockUser_AlreadyBlocked_Returns409()
    {
        var blocker = await _helper.RegisterUserAsync();
        var blocked = await _helper.RegisterUserAsync();

        // Block first time
        var request1 = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/blocks/{blocked.UserId}",
            blocker.AccessToken);
        var response1 = await _fixture.Client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Block again
        var request2 = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/blocks/{blocked.UserId}",
            blocker.AccessToken);
        var response2 = await _fixture.Client.SendAsync(request2);

        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task BlockUser_RemovesExistingFriendship()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();

        // Become friends first
        var sendResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            user1.AccessToken,
            new { userId = user2.UserId });
        var friendship = await sendResponse.ReadAsJsonAsync<JsonElement>();
        var friendshipId = friendship.GetProperty("id").ReadLong();

        var acceptRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/friends/{friendshipId}/accept",
            user2.AccessToken);
        var acceptResponse = await _fixture.Client.SendAsync(acceptRequest);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Block user2
        var blockRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/blocks/{user2.UserId}",
            user1.AccessToken);
        var blockResponse = await _fixture.Client.SendAsync(blockRequest);
        blockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify friendship is no longer in accepted friends list
        var listResponse = await _helper.AuthGetAsync(
            "/api/v1/users/@me/friends",
            user1.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var friends = await listResponse.ReadAsJsonAsync<JsonElement>();
        var friendArray = friends.GetProperty("friendships").EnumerateArray().ToList();
        friendArray.Should().NotContain(f => f.GetProperty("id").ReadLong() == friendshipId);
    }

    // ──────────── Unblock User ────────────

    [Fact]
    public async Task UnblockUser_ExistingBlock_Returns200()
    {
        var blocker = await _helper.RegisterUserAsync();
        var blocked = await _helper.RegisterUserAsync();

        // Block first
        var blockRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/blocks/{blocked.UserId}",
            blocker.AccessToken);
        var blockResponse = await _fixture.Client.SendAsync(blockRequest);
        blockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Unblock
        var unblockResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/users/@me/blocks/{blocked.UserId}",
            blocker.AccessToken);

        unblockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify user is no longer in blocks list
        var listResponse = await _helper.AuthGetAsync(
            "/api/v1/users/@me/blocks",
            blocker.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var blocks = await listResponse.ReadAsJsonAsync<JsonElement>();
        var blockArray = blocks.EnumerateArray().ToList();
        blockArray.Should().NotContain(b => b.GetProperty("blockedId").ReadLong() == blocked.UserId);
    }

    [Fact]
    public async Task UnblockUser_NotBlocked_Returns404()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/users/@me/blocks/{user2.UserId}",
            user1.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Blocked Users Cannot Send Friend Requests ────────────

    [Fact]
    public async Task SendFriendRequest_WhenBlockedByTarget_Returns400()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();

        // user2 blocks user1
        var blockRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/blocks/{user1.UserId}",
            user2.AccessToken);
        var blockResponse = await _fixture.Client.SendAsync(blockRequest);
        blockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // user1 tries to send friend request to user2
        var friendResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            user1.AccessToken,
            new { userId = user2.UserId });

        friendResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendFriendRequest_WhenBlockerSendsToBlocked_Returns400()
    {
        var blocker = await _helper.RegisterUserAsync();
        var blocked = await _helper.RegisterUserAsync();

        // blocker blocks the target
        var blockRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/blocks/{blocked.UserId}",
            blocker.AccessToken);
        var blockResponse = await _fixture.Client.SendAsync(blockRequest);
        blockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // blocker tries to send friend request to the blocked user
        var friendResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            blocker.AccessToken,
            new { userId = blocked.UserId });

        friendResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── List Blocks ────────────

    [Fact]
    public async Task ListBlocks_WithBlockedUsers_ReturnsBlockList()
    {
        var blocker = await _helper.RegisterUserAsync();
        var blocked1 = await _helper.RegisterUserAsync();
        var blocked2 = await _helper.RegisterUserAsync();

        // Block two users
        var req1 = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/blocks/{blocked1.UserId}",
            blocker.AccessToken);
        var resp1 = await _fixture.Client.SendAsync(req1);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK);

        var req2 = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/blocks/{blocked2.UserId}",
            blocker.AccessToken);
        var resp2 = await _fixture.Client.SendAsync(req2);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK);

        // List blocks
        var listResponse = await _helper.AuthGetAsync(
            "/api/v1/users/@me/blocks",
            blocker.AccessToken);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var blocks = await listResponse.ReadAsJsonAsync<JsonElement>();
        var blockArray = blocks.EnumerateArray().ToList();

        blockArray.Should().HaveCountGreaterThanOrEqualTo(2);
        blockArray.Should().Contain(b => b.GetProperty("blockedId").ReadLong() == blocked1.UserId);
        blockArray.Should().Contain(b => b.GetProperty("blockedId").ReadLong() == blocked2.UserId);
    }

    [Fact]
    public async Task ListBlocks_Empty_ReturnsEmptyArray()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/users/@me/blocks",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var blocks = await response.ReadAsJsonAsync<JsonElement>();
        blocks.EnumerateArray().Should().BeEmpty();
    }
}
