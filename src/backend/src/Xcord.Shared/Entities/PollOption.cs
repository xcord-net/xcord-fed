namespace Xcord.Entities;

/// <summary>
/// Represents a single option in a poll.
/// NOT soft-deleted — follows poll's lifecycle.
/// </summary>
public sealed class PollOption
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Poll ID (FK to Poll, Cascade delete).
    /// </summary>
    public long PollId { get; set; }

    /// <summary>
    /// Option text (max 100 characters).
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Optional emoji prefix (Unicode character or custom format "custom:{emojiId}").
    /// Max length 32.
    /// </summary>
    public string? EmojiUnicode { get; set; }

    /// <summary>
    /// Denormalized vote count (updated when votes are added/removed).
    /// </summary>
    public int VoteCount { get; set; } = 0;

    /// <summary>
    /// Display order (0-based index).
    /// </summary>
    public int Position { get; set; }

    // Navigation properties
    public Poll Poll { get; set; } = null!;
    public ICollection<PollVote> Votes { get; set; } = new List<PollVote>();
}
