namespace Xcord.Entities;

/// <summary>
/// Represents a subscription that crosspost-forwards messages from an announcement
/// channel to a target channel in another (or the same) server.
/// </summary>
public sealed class CrosspostSubscription : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The source announcement channel ID.
    /// </summary>
    public long SourceChannelId { get; set; }

    /// <summary>
    /// The target channel that receives crossposted messages.
    /// </summary>
    public long TargetChannelId { get; set; }

    /// <summary>
    /// The server that owns the source announcement channel.
    /// </summary>
    public long SourceServerId { get; set; }

    /// <summary>
    /// The server that owns the target channel.
    /// </summary>
    public long TargetServerId { get; set; }

    /// <summary>
    /// Subscription creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Channel SourceChannel { get; set; } = null!;
    public Channel TargetChannel { get; set; } = null!;
}
