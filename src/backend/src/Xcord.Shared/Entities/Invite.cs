namespace Xcord.Entities;

/// <summary>
/// Represents a server invite code with optional max uses and expiry.
/// </summary>
public sealed class Invite : ISoftDeletable
{
    /// <summary>
    /// Unique 8-character alphanumeric invite code (PK).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Server ID (FK to Server).
    /// </summary>
    public long ServerId { get; set; }

    /// <summary>
    /// User ID who created the invite (FK to User, nullable on user deletion).
    /// </summary>
    public long? CreatedByUserId { get; set; }

    /// <summary>
    /// Maximum number of uses (null = unlimited).
    /// </summary>
    public int? MaxUses { get; set; }

    /// <summary>
    /// Current number of uses.
    /// </summary>
    public int Uses { get; set; } = 0;

    /// <summary>
    /// Expiration timestamp (null = never expires).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Invite creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Optional group ID to auto-assign when the invite is used.
    /// </summary>
    public long? GroupId { get; set; }

    /// <summary>
    /// Optional channel ID to navigate to after joining (FK to Channel).
    /// </summary>
    public long? ChannelId { get; set; }

    // Navigation properties
    public Server Server { get; set; } = null!;
    public User? CreatedBy { get; set; }
    public Group? Group { get; set; }
    public Channel? Channel { get; set; }
}
