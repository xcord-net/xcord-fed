using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class ThreadTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public ThreadTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helper Methods ────────────

    private async Task<(long ServerId, long ChannelId, string AccessToken)> SetupServerWithChannel()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();
        return (serverId, channelId, user.AccessToken);
    }

    // ──────────── Create Thread ────────────

    [Fact]
    public async Task CreateThread_InTextChannel_Returns201()
    {
        var (serverId, channelId, token) = await SetupServerWithChannel();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/threads",
            token,
            new { title = "Test Thread", autoArchiveDurationMinutes = 1440 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var thread = await response.ReadAsJsonAsync<JsonElement>();
        thread.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        thread.GetProperty("channelId").ReadLong().Should().Be(channelId);
        thread.GetProperty("title").GetString().Should().Be("Test Thread");
        thread.GetProperty("isArchived").GetBoolean().Should().BeFalse();
        thread.GetProperty("isLocked").GetBoolean().Should().BeFalse();
        thread.GetProperty("autoArchiveDurationMinutes").GetInt32().Should().Be(1440);
        thread.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateThread_FromMessage_Returns201()
    {
        var (serverId, channelId, token) = await SetupServerWithChannel();
        var channel = await _helper.AuthGetAsync($"/api/v1/channels/{channelId}", token);
        var channelData = await channel.ReadAsJsonAsync<JsonElement>();
        var conversationId = channelData.GetProperty("conversationId").ReadLong();

        // Send a message first
        var message = await _helper.SendMessageAsync(token, conversationId, "This will be a thread starter");
        var messageId = message.GetProperty("id").ReadLong();

        // Create thread from the message
        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/threads",
            token,
            new { parentMessageId = messageId, title = "Thread from Message" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var thread = await response.ReadAsJsonAsync<JsonElement>();
        thread.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        thread.GetProperty("channelId").ReadLong().Should().Be(channelId);
        thread.GetProperty("title").GetString().Should().Be("Thread from Message");

        // Check if parentMessageId is present in the response
        if (thread.TryGetProperty("parentMessageId", out var parentMsgId))
        {
            parentMsgId.ReadLong().Should().Be(messageId);
        }
    }

    // ──────────── Get Thread ────────────

    [Fact]
    public async Task GetThread_Exists_Returns200()
    {
        var (serverId, channelId, token) = await SetupServerWithChannel();

        // Create thread
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/threads",
            token,
            new { title = "Thread to Get" });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdThread = await createResponse.ReadAsJsonAsync<JsonElement>();
        var threadId = createdThread.GetProperty("id").ReadLong();

        // Get the thread
        var getResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            token);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var thread = await getResponse.ReadAsJsonAsync<JsonElement>();
        thread.GetProperty("id").ReadLong().Should().Be(threadId);
        thread.GetProperty("title").GetString().Should().Be("Thread to Get");
    }

    // ──────────── List Threads ────────────

    [Fact]
    public async Task ListThreads_ReturnsActiveThreads()
    {
        var (serverId, channelId, token) = await SetupServerWithChannel();

        // Create an active thread
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/threads",
            token,
            new { title = "Active Thread" });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdThread = await createResponse.ReadAsJsonAsync<JsonElement>();
        var threadId = createdThread.GetProperty("id").ReadLong();

        // List threads with archived=false
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/threads?archived=false",
            token);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResponse.ReadAsJsonAsync<JsonElement>();
        var threads = body.GetProperty("threads").EnumerateArray().ToList();

        threads.Should().NotBeEmpty();
        threads.Should().Contain(t => t.GetProperty("id").ReadLong() == threadId);
        threads.Should().Contain(t => t.GetProperty("title").GetString() == "Active Thread");

        // Verify all returned threads are not archived
        foreach (var thread in threads)
        {
            thread.GetProperty("isArchived").GetBoolean().Should().BeFalse();
        }
    }

    // ──────────── Update Thread ────────────

    [Fact]
    public async Task UpdateThread_ChangeTitle_ReturnsUpdated()
    {
        var (serverId, channelId, token) = await SetupServerWithChannel();

        // Create thread
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/threads",
            token,
            new { title = "Original Title" });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdThread = await createResponse.ReadAsJsonAsync<JsonElement>();
        var threadId = createdThread.GetProperty("id").ReadLong();

        // Update title
        var updateResponse = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            token,
            new { title = "Updated Title" });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedThread = await updateResponse.ReadAsJsonAsync<JsonElement>();
        updatedThread.GetProperty("id").ReadLong().Should().Be(threadId);
        updatedThread.GetProperty("title").GetString().Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateThread_Archive_SetsArchived()
    {
        var (serverId, channelId, token) = await SetupServerWithChannel();

        // Create thread
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/threads",
            token,
            new { title = "Thread to Archive" });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdThread = await createResponse.ReadAsJsonAsync<JsonElement>();
        var threadId = createdThread.GetProperty("id").ReadLong();

        // Archive the thread
        var updateResponse = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            token,
            new { isArchived = true });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedThread = await updateResponse.ReadAsJsonAsync<JsonElement>();
        updatedThread.GetProperty("id").ReadLong().Should().Be(threadId);
        updatedThread.GetProperty("isArchived").GetBoolean().Should().BeTrue();
    }

    // ──────────── Join Thread ────────────

    [Fact]
    public async Task JoinThread_AsMember_Returns200()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();

        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();

        // Member joins server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var joinServerResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinServerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Owner creates thread
        var createThreadResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/threads",
            owner.AccessToken,
            new { title = "Thread to Join" });

        createThreadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var thread = await createThreadResponse.ReadAsJsonAsync<JsonElement>();
        var threadId = thread.GetProperty("id").ReadLong();

        // Member joins the thread
        var joinThreadResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}/members",
            member.AccessToken);

        joinThreadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ──────────── Leave Thread ────────────

    [Fact]
    public async Task LeaveThread_AsMember_Returns204()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();

        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();

        // Member joins server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var joinServerResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinServerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Owner creates thread
        var createThreadResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/threads",
            owner.AccessToken,
            new { title = "Thread to Leave" });

        createThreadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var thread = await createThreadResponse.ReadAsJsonAsync<JsonElement>();
        var threadId = thread.GetProperty("id").ReadLong();

        // Member joins the thread first
        var joinThreadResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}/members",
            member.AccessToken);

        joinThreadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Member leaves the thread
        var leaveThreadResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}/members/@me",
            member.AccessToken);

        leaveThreadResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
