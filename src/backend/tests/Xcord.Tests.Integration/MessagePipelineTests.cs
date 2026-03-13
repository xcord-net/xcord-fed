using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class MessagePipelineTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;
    private readonly SnowflakeIdGenerator _snowflake = new(workerId: 99);

    public MessagePipelineTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Sanitization ────────────

    [Fact]
    public async Task SendMessage_WhitespaceContent_Returns400()
    {
        var (_, conversationId, token) = await SetupServerWithChannel();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            token,
            new { content = "   \t\n   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Content Validation ────────────

    [Fact]
    public async Task SendMessage_TooLong_Returns400()
    {
        var (_, conversationId, token) = await SetupServerWithChannel();

        var longContent = new string('a', 4001);
        var response = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            token,
            new { content = longContent });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendMessage_ExactlyMaxLength_Succeeds()
    {
        var (_, conversationId, token) = await SetupServerWithChannel();

        var maxContent = new string('a', 4000);
        var message = await _helper.SendMessageAsync(token, conversationId, maxContent);

        message.GetProperty("content").GetString()!.Length.Should().Be(4000);
    }

    // ──────────── URL Extraction ────────────

    [Fact]
    public async Task SendMessage_WithUrl_ExtractsToMetadata()
    {
        var (_, conversationId, token) = await SetupServerWithChannel();

        var message = await _helper.SendMessageAsync(
            token, conversationId, "Check out https://example.com/page");

        var metadata = message.GetProperty("metadata");
        metadata.ValueKind.Should().NotBe(JsonValueKind.Null, "URL extraction should populate metadata");
        var metadataStr = metadata.GetString() ?? metadata.GetRawText();
        metadataStr.Should().Contain("https://example.com/page");
    }

    [Fact]
    public async Task SendMessage_MultipleUrls_ExtractsAll()
    {
        var (_, conversationId, token) = await SetupServerWithChannel();

        var message = await _helper.SendMessageAsync(
            token, conversationId, "Visit https://a.com and https://b.com");

        var metadata = message.GetProperty("metadata");
        metadata.ValueKind.Should().NotBe(JsonValueKind.Null, "URL extraction should populate metadata");
        var metadataStr = metadata.GetString() ?? metadata.GetRawText();
        metadataStr.Should().Contain("https://a.com");
        metadataStr.Should().Contain("https://b.com");
    }

    // ──────────── Mention Parsing ────────────

    [Fact]
    public async Task SendMessage_WithUserMention_CreatesMentionInDb()
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

        // Send message mentioning the member
        var message = await _helper.SendMessageAsync(
            owner.AccessToken, conversationId, $"Hey <@{member.UserId}> check this out");

        var messageId = message.GetProperty("id").ReadLong();

        // Verify mention was created in DB
        await using var db = _fixture.CreateDbContext();
        var mention = await db.Set<Mention>()
            .FirstOrDefaultAsync(m => m.MessageId == messageId && m.MentionedUserId == member.UserId);

        mention.Should().NotBeNull();
        mention!.IsEveryone.Should().BeFalse();
        mention.MentionedRoleId.Should().BeNull();
    }

    [Fact]
    public async Task SendMessage_WithEveryoneMention_CreatesMentionInDb()
    {
        var (serverId, conversationId, token) = await SetupServerWithChannel();

        var message = await _helper.SendMessageAsync(
            token, conversationId, "Attention @everyone!");

        var messageId = message.GetProperty("id").ReadLong();

        // Verify @everyone mention was created
        await using var db = _fixture.CreateDbContext();
        var mention = await db.Set<Mention>()
            .FirstOrDefaultAsync(m => m.MessageId == messageId && m.IsEveryone);

        mention.Should().NotBeNull();
    }

    // ──────────── Automod: Keyword Block ────────────

    [Fact]
    public async Task Automod_KeywordBlock_BlocksMessage()
    {
        var (serverId, conversationId, token) = await SetupServerWithChannel();

        // Create keyword block rule directly in DB
        await using (var db = _fixture.CreateDbContext())
        {
            db.AutomodRules.Add(new AutomodRule
            {
                Id = _snowflake.NextId(),
                ServerId = serverId,
                Name = "Block bad words",
                Enabled = true,
                TriggerType = AutomodTriggerType.Keyword,
                TriggerConfig = """{"keywords":["badword","forbidden"],"matchWholeWord":false}""",
                ActionType = AutomodActionType.BlockMessage,
                ActionConfig = "{}",
                ExemptBots = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Send message with blocked keyword
        var response = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            token,
            new { content = "This contains badword in it" });

        // Should be blocked by automod
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "automod keyword block should reject the message");
    }

    [Fact]
    public async Task Automod_KeywordBlock_AllowsCleanMessage()
    {
        var (serverId, conversationId, token) = await SetupServerWithChannel();

        // Create keyword block rule
        await using (var db = _fixture.CreateDbContext())
        {
            db.AutomodRules.Add(new AutomodRule
            {
                Id = _snowflake.NextId(),
                ServerId = serverId,
                Name = "Block profanity",
                Enabled = true,
                TriggerType = AutomodTriggerType.Keyword,
                TriggerConfig = """{"keywords":["profanity123"],"matchWholeWord":true}""",
                ActionType = AutomodActionType.BlockMessage,
                ActionConfig = "{}",
                ExemptBots = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Send clean message
        var message = await _helper.SendMessageAsync(token, conversationId, "This is a clean message");

        message.GetProperty("content").GetString().Should().Contain("clean message");
    }

    // ──────────── Automod: Regex Block ────────────

    [Fact]
    public async Task Automod_RegexBlock_BlocksMatchingMessage()
    {
        var (serverId, conversationId, token) = await SetupServerWithChannel();

        // Create regex block rule (blocks phone numbers)
        await using (var db = _fixture.CreateDbContext())
        {
            db.AutomodRules.Add(new AutomodRule
            {
                Id = _snowflake.NextId(),
                ServerId = serverId,
                Name = "Block phone numbers",
                Enabled = true,
                TriggerType = AutomodTriggerType.Regex,
                TriggerConfig = """{"pattern":"\\d{3}-\\d{3}-\\d{4}","caseSensitive":false}""",
                ActionType = AutomodActionType.BlockMessage,
                ActionConfig = "{}",
                ExemptBots = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Send message with phone number
        var response = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            token,
            new { content = "Call me at 555-123-4567" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "automod regex block should reject the message");
    }

    // ──────────── Automod: Link Filter ────────────

    [Fact]
    public async Task Automod_LinkFilter_BlocksBlacklistedDomain()
    {
        var (serverId, conversationId, token) = await SetupServerWithChannel();

        // Create link filter rule
        await using (var db = _fixture.CreateDbContext())
        {
            db.AutomodRules.Add(new AutomodRule
            {
                Id = _snowflake.NextId(),
                ServerId = serverId,
                Name = "Block spam domains",
                Enabled = true,
                TriggerType = AutomodTriggerType.LinkFilter,
                TriggerConfig = """{"blockedDomains":["spam.example.com","malware.test"],"allowedDomains":[]}""",
                ActionType = AutomodActionType.BlockMessage,
                ActionConfig = "{}",
                ExemptBots = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Send message with blocked domain
        var response = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            token,
            new { content = "Check out https://spam.example.com/free-stuff" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "automod link filter should reject the message");
    }

    [Fact]
    public async Task Automod_LinkFilter_AllowsSafeDomain()
    {
        var (serverId, conversationId, token) = await SetupServerWithChannel();

        // Create link filter rule that blocks specific domains
        await using (var db = _fixture.CreateDbContext())
        {
            db.AutomodRules.Add(new AutomodRule
            {
                Id = _snowflake.NextId(),
                ServerId = serverId,
                Name = "Block specific domains",
                Enabled = true,
                TriggerType = AutomodTriggerType.LinkFilter,
                TriggerConfig = """{"blockedDomains":["evil.test"],"allowedDomains":[]}""",
                ActionType = AutomodActionType.BlockMessage,
                ActionConfig = "{}",
                ExemptBots = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Send message with safe domain
        var message = await _helper.SendMessageAsync(
            token, conversationId, "Check out https://safe.example.com");

        message.GetProperty("content").GetString().Should().Contain("safe.example.com");
    }

    // ──────────── Automod: Disabled Rule ────────────

    [Fact]
    public async Task Automod_DisabledRule_DoesNotBlock()
    {
        var (serverId, conversationId, token) = await SetupServerWithChannel();

        // Create a disabled keyword block rule
        await using (var db = _fixture.CreateDbContext())
        {
            db.AutomodRules.Add(new AutomodRule
            {
                Id = _snowflake.NextId(),
                ServerId = serverId,
                Name = "Disabled rule",
                Enabled = false,
                TriggerType = AutomodTriggerType.Keyword,
                TriggerConfig = """{"keywords":["shouldnotblock"],"matchWholeWord":false}""",
                ActionType = AutomodActionType.BlockMessage,
                ActionConfig = "{}",
                ExemptBots = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Send message with the keyword - should NOT be blocked since rule is disabled
        var message = await _helper.SendMessageAsync(
            token, conversationId, "This contains shouldnotblock");

        message.GetProperty("content").GetString().Should().Contain("shouldnotblock");
    }

    // ──────────── Automod: Delete Action (Deferred) ────────────

    [Fact]
    public async Task Automod_DeleteAction_MessageCreatedThenSoftDeleted()
    {
        var (serverId, conversationId, token) = await SetupServerWithChannel();

        // Create keyword rule with DELETE action (deferred - message is persisted then deleted)
        await using (var db = _fixture.CreateDbContext())
        {
            db.AutomodRules.Add(new AutomodRule
            {
                Id = _snowflake.NextId(),
                ServerId = serverId,
                Name = "Delete offensive",
                Enabled = true,
                TriggerType = AutomodTriggerType.Keyword,
                TriggerConfig = """{"keywords":["deletethis"],"matchWholeWord":false}""",
                ActionType = AutomodActionType.DeleteMessage,
                ActionConfig = "{}",
                ExemptBots = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Send message - should succeed (message persisted) but then be soft-deleted
        var message = await _helper.SendMessageAsync(token, conversationId, "Please deletethis now");
        var messageId = message.GetProperty("id").ReadLong();

        // Verify the message was soft-deleted
        await using var db2 = _fixture.CreateDbContext();
        var dbMessage = await db2.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == messageId);

        dbMessage.Should().NotBeNull();
        dbMessage!.DeletedAt.Should().NotBeNull("message should be soft-deleted by automod deferred action");
    }

    // ──────────── Helper ────────────

    /// <summary>
    /// Creates a user, server, and channel. Returns (serverId, conversationId, accessToken).
    /// </summary>
    private async Task<(long ServerId, long ConversationId, string AccessToken)> SetupServerWithChannel()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();
        return (serverId, conversationId, user.AccessToken);
    }
}
