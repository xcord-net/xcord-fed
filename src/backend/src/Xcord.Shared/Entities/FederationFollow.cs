namespace Xcord.Entities;

/// <summary>
/// Represents a federation follow - a local channel subscribing to a remote channel on another instance.
/// </summary>
public sealed class FederationFollow : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// URL of the remote instance (e.g., "https://chat.example.com").
    /// </summary>
    public string RemoteInstanceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Local channel ID that receives crossposted messages (FK to Channel).
    /// </summary>
    public long LocalChannelId { get; set; }

    /// <summary>
    /// Remote channel ID (string because it's on another instance).
    /// </summary>
    public string RemoteChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Cached name of the remote channel.
    /// </summary>
    public string? RemoteChannelName { get; set; }

    /// <summary>
    /// User who created this federation follow (FK to User).
    /// </summary>
    public long FollowedByUserId { get; set; }

    /// <summary>
    /// Whether this follow is actively receiving messages.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Follow creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Channel LocalChannel { get; set; } = null!;
    public User FollowedByUser { get; set; } = null!;
}
