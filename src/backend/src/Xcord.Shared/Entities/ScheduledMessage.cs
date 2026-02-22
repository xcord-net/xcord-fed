namespace Xcord.Entities;

/// <summary>
/// Represents a message scheduled to be sent at a future time.
/// </summary>
public sealed class ScheduledMessage : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Conversation ID where the message will be sent (FK to Conversation).
    /// </summary>
    public long ConversationId { get; set; }

    /// <summary>
    /// Author user ID (FK to User).
    /// </summary>
    public long AuthorId { get; set; }

    /// <summary>
    /// Message content (max 4000 characters).
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata (stored as jsonb).
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When the message should be sent.
    /// </summary>
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>
    /// Whether the message has been sent.
    /// </summary>
    public bool IsSent { get; set; }

    /// <summary>
    /// When the message was actually sent (null if not yet sent).
    /// </summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public User Author { get; set; } = null!;
}
