namespace Xcord.Entities;

/// <summary>
/// Join record linking an active broadcast to a stream bot currently relaying its feed.
/// Tracks the connection status and any terminal error for observability.
/// </summary>
public sealed class BroadcastStreambot
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The broadcast being relayed (FK to Broadcast, Cascade).
    /// </summary>
    public long BroadcastId { get; set; }

    /// <summary>
    /// The stream bot relaying the broadcast (FK to StreamBot, Restrict).
    /// </summary>
    public long StreamBotId { get; set; }

    /// <summary>
    /// Current status of the relay connection.
    /// </summary>
    public BroadcastStreambotStatus Status { get; set; }

    /// <summary>
    /// Last error observed on this relay (null if none).
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Timestamp when the relay was started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the relay ended (null while active).
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    // Navigation properties
    public Broadcast Broadcast { get; set; } = null!;
    public StreamBot StreamBot { get; set; } = null!;
}

/// <summary>
/// Connection states for a broadcast-to-streambot relay.
/// </summary>
public enum BroadcastStreambotStatus
{
    Connecting,
    Active,
    Failed,
    Ended,
}
