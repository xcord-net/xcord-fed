using Xcord;

namespace Xcord.Entities;

/// <summary>
/// A configured external streaming destination (e.g., YouTube, Twitch) for a channel.
/// The stream key is encrypted at rest and decrypted only at RTMP connect time.
/// </summary>
public sealed class StreamBot : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The channel this stream bot belongs to (FK to Channel, Cascade).
    /// </summary>
    public long ChannelId { get; set; }

    /// <summary>
    /// The user who created this stream bot (FK to User, Restrict).
    /// </summary>
    public long CreatedByUserId { get; set; }

    /// <summary>
    /// Human-readable name of the stream bot.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Target streaming platform.
    /// </summary>
    public StreamBotPlatform Platform { get; set; }

    /// <summary>
    /// RTMP ingest URL for the target platform.
    /// </summary>
    public string RtmpUrl { get; set; } = string.Empty;

    /// <summary>
    /// AES-256-GCM encrypted stream key.
    /// </summary>
    public byte[] EncryptedStreamKey { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Whether this stream bot is engaged automatically when a broadcast starts.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Stream bot creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Channel Channel { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}

/// <summary>
/// Supported external streaming platforms.
/// </summary>
public enum StreamBotPlatform
{
    YouTube,
    Twitch,
    Rumble,
    Custom,
}
