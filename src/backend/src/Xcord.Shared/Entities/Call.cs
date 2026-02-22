using Xcord.Entities;

namespace Xcord.Entities;

/// <summary>
/// Represents a 1:1 direct call (voice/video) in a DM channel.
/// NOT soft-deleted - completed calls remain as records.
/// </summary>
public sealed class Call
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to DmChannel where the call is happening.
    /// </summary>
    public long DmChannelId { get; set; }

    /// <summary>
    /// Foreign key to User who initiated the call.
    /// </summary>
    public long CallerId { get; set; }

    /// <summary>
    /// Current status of the call.
    /// </summary>
    public CallStatus Status { get; set; }

    /// <summary>
    /// Timestamp when the call was initiated.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the call was answered (null if never answered).
    /// </summary>
    public DateTimeOffset? AnsweredAt { get; set; }

    /// <summary>
    /// Timestamp when the call ended (null if still active).
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    // Navigation properties
    public DmChannel DmChannel { get; set; } = null!;
    public User Caller { get; set; } = null!;
}
