namespace Xcord.Entities;

/// <summary>
/// Private note about another user, visible only to the note author.
/// Uses unique constraint on (AuthorId, TargetUserId) - one note per target user.
/// </summary>
public sealed class UserNote : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// User ID of the note author (FK to User).
    /// </summary>
    public long AuthorId { get; set; }

    /// <summary>
    /// User ID of the target user the note is about (FK to User).
    /// </summary>
    public long TargetUserId { get; set; }

    /// <summary>
    /// Note content (max 2000 characters).
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Note creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public User Author { get; set; } = null!;
    public User TargetUser { get; set; } = null!;
}
