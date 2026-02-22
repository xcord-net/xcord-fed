namespace Xcord.Entities;

/// <summary>
/// Represents a member timeout (mute).
/// Not soft-deleted - validity is checked via ExpiresAt.
/// </summary>
public sealed class Timeout
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
    /// Moderator ID (FK to User, nullable to preserve timeout records when moderator is deleted).
    /// </summary>
    public long? ModeratorId { get; set; }

    /// <summary>
    /// Timeout expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Reason for the timeout (max 512 characters).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Timestamp when the timeout was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Server Server { get; set; } = null!;
    public User? Moderator { get; set; }
}
