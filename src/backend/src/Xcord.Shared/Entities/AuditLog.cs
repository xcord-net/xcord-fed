namespace Xcord.Entities;

/// <summary>
/// Represents an audit log entry for server moderation actions.
/// Not soft-deleted - permanent audit trail.
/// </summary>
public sealed class AuditLog
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Server ID (FK to Server).
    /// </summary>
    public long ServerId { get; set; }

    /// <summary>
    /// Actor user ID (FK to User, nullable to preserve audit records when actor is deleted).
    /// </summary>
    public long? ActorId { get; set; }

    /// <summary>
    /// Action type (e.g., "member.ban", "role.create", "channel.delete").
    /// Max 50 characters.
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Target entity ID (the affected entity, optional).
    /// </summary>
    public long? TargetId { get; set; }

    /// <summary>
    /// Changes (before/after, stored as JSONB).
    /// </summary>
    public string? Changes { get; set; }

    /// <summary>
    /// Reason for the action (max 512 characters).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Timestamp when the action was performed.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public Server Server { get; set; } = null!;
    public User? Actor { get; set; }
}
