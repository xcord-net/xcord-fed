using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Verifies that system messages (MemberJoin, MemberLeave) are created in the server's
/// system channel when members join or leave via the standard API flows.
/// </summary>
[Collection("WebApp")]
public class SystemMessageTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public SystemMessageTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the conversationId of the first channel in the server (the auto-created
    /// "general" system channel, position 0).
    /// </summary>
    private async Task<long> GetSystemChannelConversationIdAsync(string accessToken, long serverId)
    {
        var response = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/channels", accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"listing channels should succeed: {await response.Content.ReadAsStringAsync()}");

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var channels = body.GetProperty("channels").EnumerateArray().ToList();
        channels.Should().NotBeEmpty("a newly created server should have at least one channel");

        // The auto-created "general" channel is at position 0
        return channels[0].GetProperty("conversationId").ReadLong();
    }

    /// <summary>
    /// Fetches all messages from a conversation and returns them as a list.
    /// </summary>
    private async Task<List<JsonElement>> GetMessagesAsync(string accessToken, long conversationId)
    {
        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"fetching messages should succeed: {await response.Content.ReadAsStringAsync()}");

        var body = await response.ReadAsJsonAsync<JsonElement>();
        return body.GetProperty("messages").EnumerateArray().ToList();
    }

    // ─── MemberJoin ───────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinViaInvite_CreatesSystemMessage_WithMemberJoinType()
    {
        var owner = await _helper.RegisterUserAsync();
        var joiner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var systemConversationId = await GetSystemChannelConversationIdAsync(owner.AccessToken, serverId);

        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var joinResponse = await _helper.JoinServerAsync(joiner.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK, "join should succeed");

        // Owner fetches messages from the system channel
        var messages = await GetMessagesAsync(owner.AccessToken, systemConversationId);

        messages.Should().NotBeEmpty("a MemberJoin system message should have been created");
        var systemMessage = messages.First();

        systemMessage.GetProperty("type").GetString().Should().Be("MemberJoin",
            "the system message type should be MemberJoin");
        systemMessage.GetProperty("content").GetString().Should().BeEmpty(
            "system messages have empty content");
    }

    [Fact]
    public async Task JoinViaInvite_SystemMessage_ContainsJoiningUserMetadata()
    {
        var owner = await _helper.RegisterUserAsync();
        var joiner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var systemConversationId = await GetSystemChannelConversationIdAsync(owner.AccessToken, serverId);

        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(joiner.AccessToken, inviteCode);

        var messages = await GetMessagesAsync(owner.AccessToken, systemConversationId);

        messages.Should().NotBeEmpty();
        var systemMessage = messages.First();

        // Metadata is a JSON string containing the joining user's info
        var metadataRaw = systemMessage.GetProperty("metadata").GetString();
        metadataRaw.Should().NotBeNullOrEmpty("system messages store user info in metadata");

        var metadata = JsonSerializer.Deserialize<JsonElement>(metadataRaw!);
        // The metadata is serialized with default (PascalCase) JSON options by the handler
        metadata.GetProperty("UserId").GetString().Should().Be(joiner.UserId.ToString(),
            "metadata should contain the joining user's ID");
        metadata.GetProperty("Username").GetString().Should().Be(joiner.Username,
            "metadata should contain the joining user's username");
    }

    [Fact]
    public async Task JoinViaInvite_SystemMessage_HasNullAuthorId()
    {
        var owner = await _helper.RegisterUserAsync();
        var joiner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var systemConversationId = await GetSystemChannelConversationIdAsync(owner.AccessToken, serverId);

        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(joiner.AccessToken, inviteCode);

        var messages = await GetMessagesAsync(owner.AccessToken, systemConversationId);

        messages.Should().NotBeEmpty();
        var systemMessage = messages.First();

        systemMessage.GetProperty("authorId").ValueKind.Should().Be(JsonValueKind.Null,
            "system messages are not authored by any user");
    }

    // ─── MemberLeave ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveServer_CreatesSystemMessage_WithMemberLeaveType()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var systemConversationId = await GetSystemChannelConversationIdAsync(owner.AccessToken, serverId);

        // Join first
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Count messages after join (MemberJoin message exists)
        var messagesAfterJoin = await GetMessagesAsync(owner.AccessToken, systemConversationId);
        messagesAfterJoin.Should().HaveCount(1, "exactly one MemberJoin message should exist after join");

        // Leave
        var leaveResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/@me", member.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, "leave should succeed");

        // Fetch messages again - both MemberJoin and MemberLeave should now exist
        var messagesAfterLeave = await GetMessagesAsync(owner.AccessToken, systemConversationId);
        messagesAfterLeave.Should().HaveCount(2, "both MemberJoin and MemberLeave messages should exist");

        // Newest message is first (ordered by ID descending)
        var leaveMessage = messagesAfterLeave.First();
        leaveMessage.GetProperty("type").GetString().Should().Be("MemberLeave",
            "the newest system message type should be MemberLeave");
        leaveMessage.GetProperty("content").GetString().Should().BeEmpty(
            "system messages have empty content");
        leaveMessage.GetProperty("authorId").ValueKind.Should().Be(JsonValueKind.Null,
            "system messages are not authored by any user");
    }

    [Fact]
    public async Task LeaveServer_SystemMessage_ContainsLeavingUserMetadata()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var systemConversationId = await GetSystemChannelConversationIdAsync(owner.AccessToken, serverId);

        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        await _helper.AuthDeleteAsync($"/api/v1/servers/{serverId}/members/@me", member.AccessToken);

        var messages = await GetMessagesAsync(owner.AccessToken, systemConversationId);

        // Newest is first - find the MemberLeave message
        var leaveMessage = messages.First(m =>
            m.GetProperty("type").GetString() == "MemberLeave");

        var metadataRaw = leaveMessage.GetProperty("metadata").GetString();
        metadataRaw.Should().NotBeNullOrEmpty("system messages store user info in metadata");

        var metadata = JsonSerializer.Deserialize<JsonElement>(metadataRaw!);
        // The metadata is serialized with default (PascalCase) JSON options by the handler
        metadata.GetProperty("UserId").GetString().Should().Be(member.UserId.ToString(),
            "metadata should contain the leaving user's ID");
        metadata.GetProperty("Username").GetString().Should().Be(member.Username,
            "metadata should contain the leaving user's username");
    }
}
