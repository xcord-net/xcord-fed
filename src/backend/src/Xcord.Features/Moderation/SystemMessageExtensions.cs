using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Moderation;

public static class SystemMessageExtensions
{
    public static async Task SendModerationSystemMessage(
        this AppDbContext dbContext,
        SnowflakeIdGenerator snowflakeGenerator,
        IOutboxWriter outboxWriter,
        Server server,
        long targetUserId,
        long moderatorId,
        MessageType messageType,
        string? reason,
        DateTimeOffset createdAt,
        CancellationToken ct)
    {
        if (!server.SystemChannelId.HasValue)
        {
            return;
        }

        var systemChannel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == server.SystemChannelId.Value, ct);

        if (systemChannel == null)
        {
            return;
        }

        var targetUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);

        var systemMessageId = snowflakeGenerator.NextId();
        var systemMessage = new Message
        {
            Id = systemMessageId,
            ConversationId = systemChannel.ConversationId,
            AuthorId = null,
            Type = messageType,
            Content = string.Empty,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                UserId = targetUserId.ToString(),
                Username = targetUser?.Username ?? string.Empty,
                DisplayName = targetUser?.DisplayName ?? string.Empty,
                ModeratorId = moderatorId.ToString(),
                Reason = reason
            }),
            CreatedAt = createdAt
        };

        dbContext.Messages.Add(systemMessage);

        await outboxWriter.WriteAsync(dbContext, "Message.Created", new
        {
            MessageId = systemMessage.Id,
            ConversationId = systemMessage.ConversationId,
            AuthorId = (long?)null
        }, ct);
    }
}
