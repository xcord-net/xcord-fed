namespace Xcord.Entities;

/// <summary>
/// Represents a user blocking another user.
/// Uses composite primary key (BlockerId, BlockedId).
/// NOT soft-deleted (hard-deleted on unblock).
/// </summary>
public sealed class UserBlock
{
    /// <summary>
    /// User ID of the blocker.
    /// </summary>
    public long BlockerId { get; set; }

    /// <summary>
    /// User ID of the blocked user.
    /// </summary>
    public long BlockedId { get; set; }

    /// <summary>
    /// Timestamp when the block was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public User Blocker { get; set; } = null!;
    public User Blocked { get; set; } = null!;
}
