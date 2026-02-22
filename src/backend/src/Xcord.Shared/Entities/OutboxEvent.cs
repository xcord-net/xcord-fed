namespace Xcord.Entities;

/// <summary>
/// Represents an outbox event for transactional event dispatch.
/// This entity does NOT implement ISoftDeletable - it's hard-deleted after retention period.
/// </summary>
public sealed class OutboxEvent
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Event type (e.g., "Message.Created", "Channel.Updated", "Member.Joined").
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Event payload (stored as jsonb).
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Event creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Processing timestamp (null until processed).
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>
    /// Timestamp of the last retry attempt (used for exponential backoff).
    /// </summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>
    /// Number of retry attempts (default 0).
    /// </summary>
    public int RetryCount { get; set; }
}
