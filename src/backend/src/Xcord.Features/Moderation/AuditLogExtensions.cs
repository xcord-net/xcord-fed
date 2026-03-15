using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Moderation;

/// <summary>
/// Shared helper for adding audit log entries for moderation actions.
/// </summary>
public static class AuditLogExtensions
{
    /// <summary>
    /// Adds an audit log entry to the DbSet.
    /// The entry is not saved until SaveChangesAsync is called.
    /// </summary>
    public static void AddEntry(
        this DbSet<AuditLog> auditLogs,
        SnowflakeIdGenerator snowflakeGenerator,
        long serverId,
        long actorId,
        string actionType,
        long targetId,
        string? reason,
        DateTimeOffset createdAt)
    {
        auditLogs.Add(new AuditLog
        {
            Id = snowflakeGenerator.NextId(),
            ServerId = serverId,
            ActorId = actorId,
            ActionType = actionType,
            TargetId = targetId,
            Reason = reason,
            CreatedAt = createdAt
        });
    }
}
