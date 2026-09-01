using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Moderation;

/// <summary>
/// The created system message. Carries the entity, not just its ids, so callers can
/// broadcast a payload the client can actually render.
/// </summary>
public sealed record SystemMessageInfo(long MessageId, long ConversationId, Message Message);

public static class SystemMessageExtensions
{
    /// <summary>
    /// Adds a moderation system message to the context and returns the message and conversation IDs,
    /// or null if the server has no system channel. The caller must call SaveChangesAsync and then
    /// notify the conversation using the returned info.
    /// </summary>
    public static async Task<SystemMessageInfo?> AddModerationSystemMessage(
        this AppDbContext dbContext,
        SnowflakeIdGenerator snowflakeGenerator,
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
            return null;
        }

        var systemChannel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == server.SystemChannelId.Value, ct);

        if (systemChannel == null)
        {
            return null;
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

        return new SystemMessageInfo(systemMessage.Id, systemMessage.ConversationId, systemMessage);
    }
}
