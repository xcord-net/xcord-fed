using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Tests.Integration.Fixtures;
using Xcord;
using Xunit;
using System.Text;
using System.Security.Cryptography;

namespace Xcord.Tests.Integration;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class ConversationModelTests
{
    private readonly IntegrationFixture _fixture;
    private readonly SnowflakeIdGenerator _snowflake = new(1);

    public ConversationModelTests(IntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    private static byte[] EmailToBytes(string email) => Encoding.UTF8.GetBytes(email);
    private static byte[] EmailToHash(string email) => SHA256.HashData(Encoding.UTF8.GetBytes(email));

    [Fact]
    public async Task ChannelCreatesConversation()
    {
        using var db = _fixture.CreateFreshDbContext();

        // Create user and server
        var userId = _snowflake.NextId();
        var email = "conv@test.com";
        db.Users.Add(new User
        {
            Id = userId, Username = "conv_owner", DisplayName = "Conv Owner",
            Email = EmailToBytes(email), EmailHash = EmailToHash(email),
            PasswordHash = "$2a$11$abcdef", CreatedAt = DateTimeOffset.UtcNow
        });

        var serverId = _snowflake.NextId();
        db.Servers.Add(new Server
        {
            Id = serverId, Name = "Conv Server", OwnerId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Create conversation + channel
        var conversationId = _snowflake.NextId();
        db.Conversations.Add(new Conversation
        {
            Id = conversationId, Type = ConversationType.Channel
        });

        var channelId = _snowflake.NextId();
        db.Channels.Add(new Channel
        {
            Id = channelId, ServerId = serverId, Name = "general",
            Type = ChannelType.Text, Position = 0,
            ConversationId = conversationId, CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var channel = await db.Channels
            .Include(c => c.Conversation)
            .FirstOrDefaultAsync(c => c.Id == channelId);

        channel.Should().NotBeNull();
        channel!.Conversation.Should().NotBeNull();
        channel.Conversation.Type.Should().Be(ConversationType.Channel);
    }

    [Fact]
    public async Task MessagesReferenceConversationNotChannel()
    {
        using var db = _fixture.CreateFreshDbContext();

        var userId = _snowflake.NextId();
        var email = "msg@test.com";
        db.Users.Add(new User
        {
            Id = userId, Username = "msg_author", DisplayName = "Msg Author",
            Email = EmailToBytes(email), EmailHash = EmailToHash(email),
            PasswordHash = "$2a$11$abcdef", CreatedAt = DateTimeOffset.UtcNow
        });

        var serverId = _snowflake.NextId();
        db.Servers.Add(new Server
        {
            Id = serverId, Name = "Msg Server", OwnerId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var conversationId = _snowflake.NextId();
        db.Conversations.Add(new Conversation
        {
            Id = conversationId, Type = ConversationType.Channel
        });

        db.Channels.Add(new Channel
        {
            Id = _snowflake.NextId(), ServerId = serverId, Name = "text",
            Type = ChannelType.Text, Position = 0,
            ConversationId = conversationId, CreatedAt = DateTimeOffset.UtcNow
        });

        // Message references ConversationId
        var messageId = _snowflake.NextId();
        db.Messages.Add(new Message
        {
            Id = messageId, ConversationId = conversationId,
            AuthorId = userId, Content = "Hello world",
            Type = MessageType.Default, CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var message = await db.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
        message.Should().NotBeNull();
        message!.ConversationId.Should().Be(conversationId);
    }
}
