namespace Xcord.Entities;

/// <summary>
/// A slot on the broadcast stage occupied by a user.
/// Stage slots are ordered by SlotIndex and determine layout placement.
/// </summary>
public sealed class BroadcastStageSlot
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The broadcast that owns this slot (FK to Broadcast, Cascade).
    /// </summary>
    public long BroadcastId { get; set; }

    /// <summary>
    /// The user occupying this slot (FK to User, Restrict).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Zero-based position of the slot within the broadcast stage.
    /// </summary>
    public int SlotIndex { get; set; }

    /// <summary>
    /// Timestamp when the user was added to the stage.
    /// </summary>
    public DateTimeOffset AddedAt { get; set; }

    // Navigation properties
    public Broadcast Broadcast { get; set; } = null!;
    public User User { get; set; } = null!;
}
