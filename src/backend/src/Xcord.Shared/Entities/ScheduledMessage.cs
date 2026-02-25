namespace Xcord.Entities;

/// <summary>
/// Represents a message scheduled for future delivery.
/// </summary>
public sealed class ScheduledMessage : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Conversation ID (FK to Conversation, Cascade delete).
    /// </summary>
    public long ConversationId { get; set; }

    /// <summary>
    /// Author user ID (FK to User, Cascade delete).
    /// </summary>
    public long AuthorId { get; set; }

    /// <summary>
    /// Message content (max 4000 characters).
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the message should be dispatched.
    /// </summary>
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>
    /// When the message was actually sent (null if not yet dispatched).
    /// </summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public User Author { get; set; } = null!;
}
