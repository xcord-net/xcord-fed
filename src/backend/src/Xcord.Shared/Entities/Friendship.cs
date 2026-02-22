using Xcord.Entities;

namespace Xcord.Entities;

/// <summary>
/// Represents a friendship or friend request between two users.
/// </summary>
public sealed class Friendship : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// User ID of the friend request sender.
    /// </summary>
    public long SenderId { get; set; }

    /// <summary>
    /// User ID of the friend request receiver.
    /// </summary>
    public long ReceiverId { get; set; }

    /// <summary>
    /// Current status of the friendship (Pending or Accepted).
    /// </summary>
    public FriendshipStatus Status { get; set; }

    /// <summary>
    /// Timestamp when the friendship was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
}
