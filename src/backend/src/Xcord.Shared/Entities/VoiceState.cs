namespace Xcord.Entities;

/// <summary>
/// Represents a user's current voice channel state.
/// Composite PK on UserId + ChannelId.
/// NOT soft-deleted - removed on disconnect.
/// </summary>
public sealed class VoiceState
{
    /// <summary>
    /// User ID (FK to User, part of composite PK).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Channel ID (FK to Channel, part of composite PK).
    /// </summary>
    public long ChannelId { get; set; }

    /// <summary>
    /// Whether the user has self-muted their audio.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Whether the user has self-deafened (implies muted).
    /// </summary>
    public bool IsDeafened { get; set; }

    /// <summary>
    /// Whether the user is screen sharing or Go Live streaming.
    /// </summary>
    public bool IsStreaming { get; set; }

    /// <summary>
    /// Whether the user has been server-muted by a moderator.
    /// </summary>
    public bool IsServerMuted { get; set; }

    /// <summary>
    /// Whether the user has been server-deafened by a moderator.
    /// </summary>
    public bool IsServerDeafened { get; set; }

    /// <summary>
    /// Timestamp when the user joined this voice channel.
    /// </summary>
    public DateTimeOffset JoinedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Channel Channel { get; set; } = null!;
}
