namespace Xcord.Entities;

/// <summary>
/// Represents a server ban.
/// </summary>
public sealed class Ban : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// User ID (FK to User).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Server ID (FK to Server).
    /// </summary>
    public long ServerId { get; set; }

    /// <summary>
    /// Moderator ID (FK to User, nullable to preserve ban records when moderator is deleted).
    /// </summary>
    public long? ModeratorId { get; set; }

    /// <summary>
    /// Reason for the ban (max 512 characters).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Number of days of messages to delete (1-7).
    /// </summary>
    public int? DeleteMessageDays { get; set; }

    /// <summary>
    /// Timestamp when the ban was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Server Server { get; set; } = null!;
    public User? Moderator { get; set; }
}
