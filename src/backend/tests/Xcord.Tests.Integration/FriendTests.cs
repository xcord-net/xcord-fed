using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class FriendTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public FriendTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Send Friend Request ────────────

    [Fact]
    public async Task SendFriendRequest_ById_Returns200()
    {
        var sender = await _helper.RegisterUserAsync();
        var receiver = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = receiver.UserId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("senderId").ReadLong().Should().Be(sender.UserId);
        body.GetProperty("receiverId").ReadLong().Should().Be(receiver.UserId);
        body.GetProperty("senderUsername").GetString().Should().Be(sender.Username);
        body.GetProperty("receiverUsername").GetString().Should().Be(receiver.Username);
        body.GetProperty("status").GetString().Should().Be("Pending");
    }

    [Fact]
    public async Task SendFriendRequest_ByUsername_Returns200()
    {
        var sender = await _helper.RegisterUserAsync();
        var receiver = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/friends/request",
            sender.AccessToken,
            new { username = receiver.Username });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("senderId").ReadLong().Should().Be(sender.UserId);
        body.GetProperty("receiverId").ReadLong().Should().Be(receiver.UserId);
        body.GetProperty("status").GetString().Should().Be("Pending");
    }

    [Fact]
    public async Task SendFriendRequest_ToSelf_Returns400()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            user.AccessToken,
            new { userId = user.UserId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendFriendRequest_Duplicate_Returns409()
    {
        var sender = await _helper.RegisterUserAsync();
        var receiver = await _helper.RegisterUserAsync();

        // First request
        var response1 = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = receiver.UserId });

        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Duplicate request
        var response2 = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = receiver.UserId });

        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SendFriendRequest_ToNonExistentUser_Returns404()
    {
        var sender = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = 999999999999L });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Accept Friend Request ────────────

    [Fact]
    public async Task AcceptFriendRequest_AsReceiver_Returns200()
    {
        var sender = await _helper.RegisterUserAsync();
        var receiver = await _helper.RegisterUserAsync();

        // Send request
        var sendResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = receiver.UserId });

        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var friendship = await sendResponse.ReadAsJsonAsync<JsonElement>();
        var friendshipId = friendship.GetProperty("id").ReadLong();

        // Accept request
        var request = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/friends/{friendshipId}/accept",
            receiver.AccessToken);
        var acceptResponse = await _fixture.Client.SendAsync(request);

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await acceptResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(friendshipId);
        body.GetProperty("status").GetString().Should().Be("Accepted");
        body.GetProperty("senderId").ReadLong().Should().Be(sender.UserId);
        body.GetProperty("receiverId").ReadLong().Should().Be(receiver.UserId);
    }

    [Fact]
    public async Task AcceptFriendRequest_AsSender_Returns403()
    {
        var sender = await _helper.RegisterUserAsync();
        var receiver = await _helper.RegisterUserAsync();

        // Send request
        var sendResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = receiver.UserId });

        var friendship = await sendResponse.ReadAsJsonAsync<JsonElement>();
        var friendshipId = friendship.GetProperty("id").ReadLong();

        // Sender tries to accept their own request
        var request = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/friends/{friendshipId}/accept",
            sender.AccessToken);
        var acceptResponse = await _fixture.Client.SendAsync(request);

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AcceptFriendRequest_NonExistent_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var request = TestHelper.AuthRequest(
            HttpMethod.Put,
            "/api/v1/users/@me/friends/999999999999/accept",
            user.AccessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Decline Friend Request ────────────

    [Fact]
    public async Task DeclineFriendRequest_AsReceiver_Returns200()
    {
        var sender = await _helper.RegisterUserAsync();
        var receiver = await _helper.RegisterUserAsync();

        // Send request
        var sendResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = receiver.UserId });

        var friendship = await sendResponse.ReadAsJsonAsync<JsonElement>();
        var friendshipId = friendship.GetProperty("id").ReadLong();

        // Decline (remove) the pending request as the receiver
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/users/@me/friends/{friendshipId}",
            receiver.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ──────────── Remove Friend ────────────

    [Fact]
    public async Task RemoveFriend_AcceptedFriendship_Returns200()
    {
        var sender = await _helper.RegisterUserAsync();
        var receiver = await _helper.RegisterUserAsync();

        // Send and accept friend request
        var sendResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = receiver.UserId });

        var friendship = await sendResponse.ReadAsJsonAsync<JsonElement>();
        var friendshipId = friendship.GetProperty("id").ReadLong();

        var acceptRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/friends/{friendshipId}/accept",
            receiver.AccessToken);
        var acceptResponse = await _fixture.Client.SendAsync(acceptRequest);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Remove friendship
        var removeResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/users/@me/friends/{friendshipId}",
            sender.AccessToken);

        removeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify friendship no longer appears in friends list
        var listResponse = await _helper.AuthGetAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var friends = await listResponse.ReadAsJsonAsync<JsonElement>();
        var friendArray = friends.EnumerateArray().ToList();
        friendArray.Should().NotContain(f => f.GetProperty("id").ReadLong() == friendshipId);
    }

    [Fact]
    public async Task RemoveFriend_AsNonParticipant_Returns403()
    {
        var sender = await _helper.RegisterUserAsync();
        var receiver = await _helper.RegisterUserAsync();
        var outsider = await _helper.RegisterUserAsync();

        // Send request
        var sendResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = receiver.UserId });

        var friendship = await sendResponse.ReadAsJsonAsync<JsonElement>();
        var friendshipId = friendship.GetProperty("id").ReadLong();

        // Outsider tries to remove
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/users/@me/friends/{friendshipId}",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveFriend_NonExistent_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthDeleteAsync(
            "/api/v1/users/@me/friends/999999999999",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── List Friends ────────────

    [Fact]
    public async Task ListFriends_Accepted_ReturnsOnlyAcceptedFriends()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();
        var user3 = await _helper.RegisterUserAsync();

        // Create and accept friendship with user2
        var send1 = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            user1.AccessToken,
            new { userId = user2.UserId });
        var f1 = await send1.ReadAsJsonAsync<JsonElement>();
        var f1Id = f1.GetProperty("id").ReadLong();

        var accept1 = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/friends/{f1Id}/accept",
            user2.AccessToken);
        var acceptResponse = await _fixture.Client.SendAsync(accept1);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Create pending friendship with user3 (do NOT accept)
        await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            user1.AccessToken,
            new { userId = user3.UserId });

        // List accepted friends (default status)
        var listResponse = await _helper.AuthGetAsync(
            "/api/v1/users/@me/friends",
            user1.AccessToken);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var friends = await listResponse.ReadAsJsonAsync<JsonElement>();
        var friendArray = friends.EnumerateArray().ToList();

        // Should contain the accepted friendship but not the pending one
        friendArray.Should().Contain(f => f.GetProperty("id").ReadLong() == f1Id);
        friendArray.All(f => f.GetProperty("status").GetString() == "Accepted").Should().BeTrue();
    }

    [Fact]
    public async Task ListFriends_Pending_ReturnsPendingRequests()
    {
        var sender = await _helper.RegisterUserAsync();
        var receiver = await _helper.RegisterUserAsync();

        // Send friend request
        var sendResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = receiver.UserId });
        var friendship = await sendResponse.ReadAsJsonAsync<JsonElement>();
        var friendshipId = friendship.GetProperty("id").ReadLong();

        // List pending requests (as sender)
        var listResponse = await _helper.AuthGetAsync(
            "/api/v1/users/@me/friends?status=Pending",
            sender.AccessToken);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var friends = await listResponse.ReadAsJsonAsync<JsonElement>();
        var friendArray = friends.EnumerateArray().ToList();

        friendArray.Should().Contain(f => f.GetProperty("id").ReadLong() == friendshipId);
        friendArray.All(f => f.GetProperty("status").GetString() == "Pending").Should().BeTrue();
    }

    [Fact]
    public async Task ListFriends_Empty_ReturnsEmptyArray()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/users/@me/friends",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var friends = await response.ReadAsJsonAsync<JsonElement>();
        friends.EnumerateArray().Should().BeEmpty();
    }

    // ──────────── Duplicate: Already Friends ────────────

    [Fact]
    public async Task SendFriendRequest_AlreadyFriends_Returns409()
    {
        var sender = await _helper.RegisterUserAsync();
        var receiver = await _helper.RegisterUserAsync();

        // Send and accept
        var sendResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = receiver.UserId });
        var friendship = await sendResponse.ReadAsJsonAsync<JsonElement>();
        var friendshipId = friendship.GetProperty("id").ReadLong();

        var acceptRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/friends/{friendshipId}/accept",
            receiver.AccessToken);
        var acceptResponse = await _fixture.Client.SendAsync(acceptRequest);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Try sending another friend request
        var duplicateResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/friends",
            sender.AccessToken,
            new { userId = receiver.UserId });

        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
