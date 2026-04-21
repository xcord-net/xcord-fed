using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a live broadcast session in a streaming channel.
/// A broadcast is created when a host goes live and closed when the stream ends.
/// </summary>
public sealed class Broadcast : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The channel this broadcast belongs to (FK to Channel, Cascade).
    /// </summary>
    public long ChannelId { get; set; }

    /// <summary>
    /// The user hosting this broadcast (FK to User, Restrict).
    /// </summary>
    public long HostUserId { get; set; }

    /// <summary>
    /// LiveKit egress job identifier.
    /// </summary>
    public string EgressJobId { get; set; } = string.Empty;

    /// <summary>
    /// Composition layout preset applied by the egress compositor.
    /// </summary>
    public BroadcastLayoutPreset LayoutPreset { get; set; }

    /// <summary>
    /// MinIO object key for the HLS playlist.
    /// </summary>
    public string HlsPlaylistKey { get; set; } = string.Empty;

    /// <summary>
    /// Current lifecycle status of the broadcast.
    /// </summary>
    public BroadcastStatus Status { get; set; }

    /// <summary>
    /// Broadcast start timestamp.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Broadcast end timestamp (null while live).
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Channel Channel { get; set; } = null!;
    public User Host { get; set; } = null!;
    public ICollection<BroadcastStageSlot> StageSlots { get; set; } = new List<BroadcastStageSlot>();
    public ICollection<BroadcastStreambot> ActiveStreambots { get; set; } = new List<BroadcastStreambot>();
}

/// <summary>
/// Layout presets applied to the egress composition.
/// </summary>
public enum BroadcastLayoutPreset
{
    Grid,
    Spotlight,
    Pip,
    SideBySide,
}

/// <summary>
/// Lifecycle states for a broadcast.
/// </summary>
public enum BroadcastStatus
{
    Starting,
    Live,
    Ended,
    Failed,
}
