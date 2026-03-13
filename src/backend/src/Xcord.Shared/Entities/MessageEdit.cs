namespace Xcord.Entities;

/// <summary>
/// Represents a historical edit of a message.
/// Preserves the previous content before each edit for audit purposes.
/// NOT soft-deleted - edit history is preserved permanently.
/// </summary>
public sealed class MessageEdit
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Message ID (FK to Message, Cascade delete).
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// The content before the edit was applied.
    /// </summary>
    public string PreviousContent { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the edit occurred.
    /// </summary>
    public DateTimeOffset EditedAt { get; set; }

    // Navigation properties
    public Message Message { get; set; } = null!;
}
