using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a poll attached to a message.
/// One-to-one relationship with Message (MessageType.PollCreated).
/// </summary>
public sealed class Poll : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Message ID (FK to Message, unique, Cascade delete).
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// Poll question (max 300 characters).
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Whether multiple answers are allowed.
    /// </summary>
    public bool AllowMultipleAnswers { get; set; } = false;

    /// <summary>
    /// Optional expiration timestamp.
    /// Null means poll does not expire.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Whether the poll has been manually closed or auto-closed after expiry.
    /// </summary>
    public bool IsClosed { get; set; } = false;

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Message Message { get; set; } = null!;
    public ICollection<PollOption> Options { get; set; } = new List<PollOption>();
}
