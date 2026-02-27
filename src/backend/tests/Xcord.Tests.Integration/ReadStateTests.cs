using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class ReadStateTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public ReadStateTests(WebAppFixture fixture)
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

    // ──────────── Get Unread Count ────────────

    [Fact]
    public async Task GetUnreadCount_NoReadStates_ReturnsZeros()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/users/@me/unread-count",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("totalUnread").GetInt32().Should().Be(0);
        body.GetProperty("totalMentions").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetUnreadCount_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.GetAsync("/api/v1/users/@me/unread-count");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Mark As Read ────────────

    [Fact]
    public async Task MarkAsRead_ValidMessageInConversation_ReturnsReadState()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Send some messages
        var msg1 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 1");
        var msg2 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 2");
        var msg3 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 3");
        var msg3Id = msg3.GetProperty("id").ReadLong();

        // Mark conversation as read up to msg3
        var response = await AuthPutAsync(
            $"/api/v1/conversations/{conversationId}/read-state",
            user.AccessToken,
            new { messageId = msg3Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("conversationId").ReadLong().Should().Be(conversationId);
        body.GetProperty("lastReadMessageId").ReadLong().Should().Be(msg3Id);
        body.GetProperty("unreadCount").GetInt32().Should().Be(0);
        body.GetProperty("mentionCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task MarkAsRead_UpdatesExistingReadState()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var msg1 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 1");
        var msg1Id = msg1.GetProperty("id").ReadLong();
        var msg2 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 2");
        var msg2Id = msg2.GetProperty("id").ReadLong();

        // Mark as read up to msg1
        await AuthPutAsync(
            $"/api/v1/conversations/{conversationId}/read-state",
            user.AccessToken,
            new { messageId = msg1Id });

        // Mark as read up to msg2
        var response = await AuthPutAsync(
            $"/api/v1/conversations/{conversationId}/read-state",
            user.AccessToken,
            new { messageId = msg2Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("lastReadMessageId").ReadLong().Should().Be(msg2Id);
    }

    [Fact]
    public async Task MarkAsRead_NonExistentConversation_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await AuthPutAsync(
            "/api/v1/conversations/999999999999/read-state",
            user.AccessToken,
            new { messageId = 123456789L });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkAsRead_NonExistentMessage_Returns404()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var response = await AuthPutAsync(
            $"/api/v1/conversations/{conversationId}/read-state",
            user.AccessToken,
            new { messageId = 999_999_999_999L });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkAsRead_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.PutJsonAsync(
            "/api/v1/conversations/123456/read-state",
            new { messageId = 789L });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkAsRead_ThenGetUnreadCount_ReflectsReadState()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var msg = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Read this");
        var msgId = msg.GetProperty("id").ReadLong();

        // Mark as read
        await AuthPutAsync(
            $"/api/v1/conversations/{conversationId}/read-state",
            user.AccessToken,
            new { messageId = msgId });

        // Check unread count
        var unreadResponse = await _helper.AuthGetAsync(
            "/api/v1/users/@me/unread-count",
            user.AccessToken);

        unreadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await unreadResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("totalUnread").GetInt32().Should().Be(0);
        body.GetProperty("totalMentions").GetInt32().Should().Be(0);
    }
}
