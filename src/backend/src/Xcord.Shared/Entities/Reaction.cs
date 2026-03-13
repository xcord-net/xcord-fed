namespace Xcord.Entities;

/// <summary>
/// Represents a reaction to a message.
/// Composite PK: MessageId + UserId + Emoji.
/// NOT soft-deleted - reactions are hard-deleted on removal.
/// </summary>
public sealed class Reaction
{
    /// <summary>
    /// Message ID (FK to Message, Cascade delete).
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// User ID who added the reaction (FK to User, Cascade delete).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Emoji string (Unicode character or custom format "custom:{emojiId}").
    /// Max length 32.
    /// </summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the reaction was added.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public Message Message { get; set; } = null!;
    public User User { get; set; } = null!;
}
