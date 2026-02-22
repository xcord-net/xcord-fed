using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class MessageEndpointTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public MessageEndpointTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Send Message ────────────

    [Fact]
    public async Task SendMessage_ValidContent_Returns201WithMessage()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var message = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Hello world");

        message.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        message.GetProperty("conversationId").ReadLong().Should().Be(conversationId);
        message.GetProperty("authorId").ReadLong().Should().Be(user.UserId);
        message.GetProperty("content").GetString().Should().Be("Hello world");
        message.GetProperty("type").GetString().Should().Be("Default");
        message.GetProperty("isPinned").GetBoolean().Should().BeFalse();
        message.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SendMessage_AsNonMember_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var nonMember = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            nonMember.AccessToken,
            new { content = "Forbidden message" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SendMessage_EmptyContent_Returns400()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            user.AccessToken,
            new { content = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Edit Message ────────────

    [Fact]
    public async Task EditMessage_AsAuthor_ReturnsUpdatedMessage()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();
        var message = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Original content");
        var messageId = message.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/conversations/{conversationId}/messages/{messageId}",
            user.AccessToken,
            new { content = "Edited content" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.ReadAsJsonAsync<JsonElement>();
        updated.GetProperty("id").ReadLong().Should().Be(messageId);
        updated.GetProperty("content").GetString().Should().Be("Edited content");
        updated.GetProperty("editedAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task EditMessage_AsNonAuthor_Returns403()
    {
        var author = await _helper.RegisterUserAsync();
        var otherMember = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(author.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(author.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Add otherMember to server
        var inviteCode = await _helper.CreateInviteAsync(author.AccessToken, serverId);
        await _helper.JoinServerAsync(otherMember.AccessToken, inviteCode);

        // Author sends message
        var message = await _helper.SendMessageAsync(author.AccessToken, conversationId, "Original");
        var messageId = message.GetProperty("id").ReadLong();

        // Other member tries to edit
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/conversations/{conversationId}/messages/{messageId}",
            otherMember.AccessToken,
            new { content = "Hacked!" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Delete Message ────────────

    [Fact]
    public async Task DeleteMessage_AsAuthor_Returns204()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();
        var message = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Delete me");
        var messageId = message.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/conversations/{conversationId}/messages/{messageId}",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteMessage_AsOwnerNotAuthor_Returns204()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Add member to server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Member sends message
        var message = await _helper.SendMessageAsync(member.AccessToken, conversationId, "Member's message");
        var messageId = message.GetProperty("id").ReadLong();

        // Owner deletes it (has ManageMessages permission)
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/conversations/{conversationId}/messages/{messageId}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteMessage_AsNonAuthorNonOwner_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var author = await _helper.RegisterUserAsync();
        var otherMember = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Add both members
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(author.AccessToken, inviteCode);
        await _helper.JoinServerAsync(otherMember.AccessToken, inviteCode);

        // Author sends message
        var message = await _helper.SendMessageAsync(author.AccessToken, conversationId, "Message");
        var messageId = message.GetProperty("id").ReadLong();

        // Other member tries to delete (no ManageMessages permission)
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/conversations/{conversationId}/messages/{messageId}",
            otherMember.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Get Messages ────────────

    [Fact]
    public async Task GetMessages_ReturnsMessagesNewestFirst()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Send 3 messages
        var msg1 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "First");
        var msg2 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Second");
        var msg3 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Third");

        var msg1Id = msg1.GetProperty("id").ReadLong();
        var msg2Id = msg2.GetProperty("id").ReadLong();
        var msg3Id = msg3.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var messages = body.GetProperty("messages").EnumerateArray().ToList();

        messages.Should().HaveCount(3);
        messages[0].GetProperty("id").ReadLong().Should().Be(msg3Id);
        messages[0].GetProperty("content").GetString().Should().Be("Third");
        messages[1].GetProperty("id").ReadLong().Should().Be(msg2Id);
        messages[1].GetProperty("content").GetString().Should().Be("Second");
        messages[2].GetProperty("id").ReadLong().Should().Be(msg1Id);
        messages[2].GetProperty("content").GetString().Should().Be("First");
    }

    [Fact]
    public async Task GetMessages_WithLimit_RespectsLimit()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Send 5 messages
        await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 1");
        await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 2");
        await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 3");
        await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 4");
        await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 5");

        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages?limit=2",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var messages = body.GetProperty("messages").EnumerateArray().ToList();

        messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMessages_WithCursor_ReturnsBefore()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Send 5 messages
        var msg1 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 1");
        var msg2 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 2");
        var msg3 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 3");
        var msg4 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 4");
        var msg5 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Message 5");

        var msg3Id = msg3.GetProperty("id").ReadLong();
        var msg2Id = msg2.GetProperty("id").ReadLong();
        var msg1Id = msg1.GetProperty("id").ReadLong();

        // Get messages before msg3 (should return msg2 and msg1)
        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages?before={msg3Id}&limit=5",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var messages = body.GetProperty("messages").EnumerateArray().ToList();

        messages.Should().HaveCount(2);
        messages[0].GetProperty("id").ReadLong().Should().Be(msg2Id);
        messages[1].GetProperty("id").ReadLong().Should().Be(msg1Id);
    }

    // ──────────── Bulk Delete ────────────

    [Fact]
    public async Task BulkDelete_AsOwner_Returns204()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Send 3 messages
        var msg1 = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "Message 1");
        var msg2 = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "Message 2");
        var msg3 = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "Message 3");

        var messageIds = new[]
        {
            msg1.GetProperty("id").ReadLong(),
            msg2.GetProperty("id").ReadLong(),
            msg3.GetProperty("id").ReadLong()
        };

        var response = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages/bulk-delete",
            owner.AccessToken,
            new { messageIds });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
