namespace Xcord.Entities;

/// <summary>
/// Represents a user's vote on a poll option.
/// Composite PK: PollOptionId + UserId.
/// NOT soft-deleted — hard-deleted on vote retraction.
/// </summary>
public sealed class PollVote
{
    /// <summary>
    /// Poll option ID (FK to PollOption, Cascade delete).
    /// </summary>
    public long PollOptionId { get; set; }

    /// <summary>
    /// User ID who cast the vote (FK to User, Cascade delete).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Timestamp when the vote was cast.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public PollOption PollOption { get; set; } = null!;
    public User User { get; set; } = null!;
}
