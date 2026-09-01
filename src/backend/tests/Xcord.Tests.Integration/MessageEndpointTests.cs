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

        var msg2Id = msg2.GetProperty("id").ReadLong();
        var msg1Id = msg1.GetProperty("id").ReadLong();

        // First page: get the 3 newest messages (msg5, msg4, msg3) and capture the
        // server-issued opaque cursor pointing at the boundary (older than msg3).
        var firstPage = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages?limit=3",
            user.AccessToken);
        firstPage.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstBody = await firstPage.ReadAsJsonAsync<JsonElement>();
        firstBody.GetProperty("messages").EnumerateArray().Count().Should().Be(3);
        var nextCursor = firstBody.GetProperty("nextCursor").GetString();
        nextCursor.Should().NotBeNullOrEmpty();

        // Use the opaque cursor to fetch the next page (should return msg2 and msg1).
        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages?cursor={nextCursor}&limit=5",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var messages = body.GetProperty("messages").EnumerateArray().ToList();

        messages.Should().HaveCount(2);
        messages[0].GetProperty("id").ReadLong().Should().Be(msg2Id);
        messages[1].GetProperty("id").ReadLong().Should().Be(msg1Id);
    }

    [Fact]
    public async Task GetMessages_WithTamperedCursor_Returns400()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // A tampered/garbage cursor must be rejected with 400 Validation
        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages?cursor=not-a-real-cursor",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Bulk Delete ────────────

    [Fact]
    public async Task BulkDelete_AsOwner_Returns204AndDeletesMessages()
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

        var bulkDeleteResponse = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages/bulk-delete",
            owner.AccessToken,
            new { messageIds });

        bulkDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the messages are actually gone by re-fetching the conversation
        var getResponse = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            owner.AccessToken);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await getResponse.ReadAsJsonAsync<JsonElement>();
        var remaining = body.GetProperty("messages").EnumerateArray().ToList();

        // None of the bulk-deleted message IDs should appear in the response
        var remainingIds = remaining.Select(m => m.GetProperty("id").ReadLong()).ToHashSet();
        remainingIds.Should().NotContain(messageIds[0], "bulk-deleted message 1 should be absent");
        remainingIds.Should().NotContain(messageIds[1], "bulk-deleted message 2 should be absent");
        remainingIds.Should().NotContain(messageIds[2], "bulk-deleted message 3 should be absent");
    }

    // ──────────── Reply references ────────────
    //
    // Replies carry their target's author and a truncated preview so the client can
    // render a quote line, connect the exchange into a lane, and offer a jump back
    // without a second fetch.

    /// <summary>Sets up a channel with two members and returns its conversation id.</summary>
    private async Task<(AuthenticatedUser Owner, AuthenticatedUser Member, long ConversationId)> CreateSharedChannelAsync()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);

        return (owner, member, channel.GetProperty("conversationId").ReadLong());
    }

    private async Task<JsonElement> SendReplyAsync(
        string accessToken, long conversationId, long replyToId, string content)
    {
        var response = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            accessToken,
            new { content, replyToId });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"sending a reply should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task SendMessage_AsReply_ReturnsReplyTargetAuthorAndPreview()
    {
        var (owner, member, conversationId) = await CreateSharedChannelAsync();
        var parent = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "what do you think?");
        var parentId = parent.GetProperty("id").ReadLong();

        var reply = await SendReplyAsync(member.AccessToken, conversationId, parentId, "sounds good");

        var replyTo = reply.GetProperty("replyTo");
        replyTo.GetProperty("id").ReadLong().Should().Be(parentId);
        replyTo.GetProperty("authorUsername").GetString().Should().Be(owner.Username);
        replyTo.GetProperty("preview").GetString().Should().Be("what do you think?");
        replyTo.GetProperty("isDeleted").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetMessages_ReplyInList_CarriesReplyTarget()
    {
        var (owner, member, conversationId) = await CreateSharedChannelAsync();
        var parent = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "original question");
        var parentId = parent.GetProperty("id").ReadLong();
        var reply = await SendReplyAsync(member.AccessToken, conversationId, parentId, "an answer");
        var replyId = reply.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages", owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var listed = body.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("id").ReadLong() == replyId);

        listed.GetProperty("replyTo").GetProperty("preview").GetString().Should().Be("original question");
        listed.GetProperty("replyTo").GetProperty("authorUsername").GetString().Should().Be(owner.Username);
    }

    [Fact]
    public async Task GetMessages_NonReply_HasNoReplyTarget()
    {
        var (owner, _, conversationId) = await CreateSharedChannelAsync();
        var plain = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "just talking");
        var plainId = plain.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages", owner.AccessToken);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var listed = body.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("id").ReadLong() == plainId);

        listed.TryGetProperty("replyTo", out var replyTo).Should().BeTrue();
        replyTo.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetMessages_ReplyToDeletedMessage_ReportsTargetAsDeleted()
    {
        var (owner, member, conversationId) = await CreateSharedChannelAsync();
        var parent = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "soon to be gone");
        var parentId = parent.GetProperty("id").ReadLong();
        var reply = await SendReplyAsync(member.AccessToken, conversationId, parentId, "still here");
        var replyId = reply.GetProperty("id").ReadLong();

        var deleteResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/conversations/{conversationId}/messages/{parentId}", owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages", owner.AccessToken);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var listed = body.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("id").ReadLong() == replyId);

        var replyTo = listed.GetProperty("replyTo");
        replyTo.GetProperty("isDeleted").GetBoolean().Should().BeTrue();
        replyTo.GetProperty("id").ReadLong().Should().Be(parentId);
        replyTo.GetProperty("preview").GetString().Should().BeEmpty();
        replyTo.GetProperty("authorUsername").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task SendMessage_ReplyToLongMessage_TruncatesPreview()
    {
        var (owner, member, conversationId) = await CreateSharedChannelAsync();
        var longContent = new string('x', 200);
        var parent = await _helper.SendMessageAsync(owner.AccessToken, conversationId, longContent);
        var parentId = parent.GetProperty("id").ReadLong();

        var reply = await SendReplyAsync(member.AccessToken, conversationId, parentId, "tl;dr");

        var preview = reply.GetProperty("replyTo").GetProperty("preview").GetString()!;
        preview.Should().Be(new string('x', 80) + "…");
    }

    [Fact]
    public async Task SendMessage_ReplyToMultilineMessage_FlattensPreviewToOneLine()
    {
        var (owner, member, conversationId) = await CreateSharedChannelAsync();
        var parent = await _helper.SendMessageAsync(
            owner.AccessToken, conversationId, "first line\nsecond line");
        var parentId = parent.GetProperty("id").ReadLong();

        var reply = await SendReplyAsync(member.AccessToken, conversationId, parentId, "noted");

        reply.GetProperty("replyTo").GetProperty("preview").GetString()
            .Should().Be("first line second line");
    }

    [Fact]
    public async Task SendMessage_ReplyToEncodedContent_DecodesPreviewForDisplay()
    {
        // Stored content is HTML-encoded by the sanitizer; the preview must come back
        // as readable text rather than a string of numeric character references.
        var (owner, member, conversationId) = await CreateSharedChannelAsync();
        var parent = await _helper.SendMessageAsync(
            owner.AccessToken, conversationId, "5 > 3 & \"quoted\"");
        var parentId = parent.GetProperty("id").ReadLong();

        var reply = await SendReplyAsync(member.AccessToken, conversationId, parentId, "agreed");

        reply.GetProperty("replyTo").GetProperty("preview").GetString()
            .Should().Be("5 > 3 & \"quoted\"");
    }

    [Fact]
    public async Task SendMessage_ReplyToEmojiContent_TruncatesWithoutSplittingSurrogatePairs()
    {
        var (owner, member, conversationId) = await CreateSharedChannelAsync();
        // 60 emoji is 120 UTF-16 units, so the 80-char cut lands inside a pair.
        var emojiContent = string.Concat(Enumerable.Repeat("😀", 60));
        var parent = await _helper.SendMessageAsync(owner.AccessToken, conversationId, emojiContent);
        var parentId = parent.GetProperty("id").ReadLong();

        var reply = await SendReplyAsync(member.AccessToken, conversationId, parentId, "lots of those");

        var preview = reply.GetProperty("replyTo").GetProperty("preview").GetString()!;
        preview.Should().NotContain("�", "a split surrogate pair would render as U+FFFD");
        preview.Should().EndWith("…");
        char.IsLowSurrogate(preview[^2]).Should().BeTrue("the last emoji should be whole");
    }

    [Fact]
    public async Task GetMessage_SingleReply_CarriesReplyTarget()
    {
        var (owner, member, conversationId) = await CreateSharedChannelAsync();
        var parent = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "the question");
        var parentId = parent.GetProperty("id").ReadLong();
        var reply = await SendReplyAsync(member.AccessToken, conversationId, parentId, "the answer");
        var replyId = reply.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages/{replyId}", owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("replyTo").GetProperty("preview").GetString().Should().Be("the question");
    }

    // ──────────── Author group colours ────────────
    //
    // The username colour is a per-server attribute taken from the author's highest
    // coloured group. It reaches the client on the message itself and on the reply
    // target, so a quote line names its author in the same colour as the header.

    /// <summary>Creates a coloured group on the server and assigns it to a member.</summary>
    private async Task<long> CreateAndAssignGroupAsync(
        AuthenticatedUser owner, long serverId, long userId, string color, int? position = null)
    {
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            position.HasValue
                ? new { name = $"group-{color[1..]}", color, position = position.Value }
                : new { name = $"group-{color[1..]}", color });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"group creation should succeed: {await createResponse.Content.ReadAsStringAsync()}");
        var groupId = (await createResponse.ReadAsJsonAsync<JsonElement>()).GetProperty("id").ReadLong();

        var assignResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{userId}/groups/{groupId}", owner.AccessToken);
        assignResponse.IsSuccessStatusCode.Should().BeTrue(
            $"group assignment should succeed: {await assignResponse.Content.ReadAsStringAsync()}");

        return groupId;
    }

    private async Task<(AuthenticatedUser Owner, AuthenticatedUser Member, long ServerId, long ConversationId)>
        CreateSharedServerAndChannelAsync()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);

        return (owner, member, serverId, channel.GetProperty("conversationId").ReadLong());
    }

    [Fact]
    public async Task GetMessages_AuthorInColouredGroup_ReturnsAuthorGroupColor()
    {
        var (owner, _, serverId, conversationId) = await CreateSharedServerAndChannelAsync();
        await CreateAndAssignGroupAsync(owner, serverId, owner.UserId, "#FF5733");
        await _helper.SendMessageAsync(owner.AccessToken, conversationId, "coloured");

        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages", owner.AccessToken);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var listed = body.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("content").GetString() == "coloured");

        listed.GetProperty("authorGroupColor").GetString().Should().Be("#FF5733");
    }

    [Fact]
    public async Task GetMessages_AuthorWithNoColouredGroup_ReturnsNullColor()
    {
        var (owner, _, _, conversationId) = await CreateSharedServerAndChannelAsync();
        await _helper.SendMessageAsync(owner.AccessToken, conversationId, "plain");

        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages", owner.AccessToken);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var listed = body.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("content").GetString() == "plain");

        listed.GetProperty("authorGroupColor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetMessages_AuthorInSeveralGroups_UsesTheHighestPositionedColour()
    {
        var (owner, _, serverId, conversationId) = await CreateSharedServerAndChannelAsync();
        await CreateAndAssignGroupAsync(owner, serverId, owner.UserId, "#111111", position: 1);
        await CreateAndAssignGroupAsync(owner, serverId, owner.UserId, "#222222", position: 9);
        await _helper.SendMessageAsync(owner.AccessToken, conversationId, "ranked");

        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages", owner.AccessToken);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var listed = body.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("content").GetString() == "ranked");

        listed.GetProperty("authorGroupColor").GetString().Should().Be("#222222");
    }

    [Fact]
    public async Task SendMessage_ReplyToColouredAuthor_CarriesTheirColourOnTheReplyTarget()
    {
        var (owner, member, serverId, conversationId) = await CreateSharedServerAndChannelAsync();
        await CreateAndAssignGroupAsync(owner, serverId, owner.UserId, "#FF5733");
        var parent = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "quoted me");
        var parentId = parent.GetProperty("id").ReadLong();

        var reply = await SendReplyAsync(member.AccessToken, conversationId, parentId, "replying");

        reply.GetProperty("replyTo").GetProperty("authorGroupColor").GetString().Should().Be("#FF5733");
    }

    [Fact]
    public async Task ListPinnedMessages_CarriesColourAndReplyTarget()
    {
        // Pins go through a different handler than the message list; it must not
        // quietly return nulls for either.
        var (owner, member, serverId, conversationId) = await CreateSharedServerAndChannelAsync();
        await CreateAndAssignGroupAsync(owner, serverId, owner.UserId, "#FF5733");
        var parent = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "pin parent");
        var parentId = parent.GetProperty("id").ReadLong();
        var reply = await SendReplyAsync(member.AccessToken, conversationId, parentId, "pin me");
        var replyId = reply.GetProperty("id").ReadLong();

        var pinResponse = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages/{replyId}/pin", owner.AccessToken);
        pinResponse.IsSuccessStatusCode.Should().BeTrue(
            $"pinning should succeed: {await pinResponse.Content.ReadAsStringAsync()}");

        var pins = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/pins", owner.AccessToken);
        var body = await pins.ReadAsJsonAsync<JsonElement>();
        var pinned = body.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("id").ReadLong() == replyId);

        pinned.GetProperty("replyTo").GetProperty("preview").GetString().Should().Be("pin parent");
        pinned.GetProperty("replyTo").GetProperty("authorGroupColor").GetString().Should().Be("#FF5733");
    }

    [Fact]
    public async Task SearchMessages_CarriesColourAndReplyTarget()
    {
        var (owner, member, serverId, conversationId) = await CreateSharedServerAndChannelAsync();
        await CreateAndAssignGroupAsync(owner, serverId, owner.UserId, "#FF5733");
        var needle = $"needle-{Guid.NewGuid():N}";
        var parent = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "search parent");
        var parentId = parent.GetProperty("id").ReadLong();
        await SendReplyAsync(member.AccessToken, conversationId, parentId, needle);

        var searchResponse = await _helper.AuthGetAsync(
            $"/api/v1/search?query={needle}&conversationId={conversationId}", owner.AccessToken);
        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await searchResponse.ReadAsJsonAsync<JsonElement>();
        var hit = body.GetProperty("messages").EnumerateArray().Single();

        hit.GetProperty("replyTo").GetProperty("preview").GetString().Should().Be("search parent");
        hit.GetProperty("replyTo").GetProperty("authorGroupColor").GetString().Should().Be("#FF5733");
    }
}
