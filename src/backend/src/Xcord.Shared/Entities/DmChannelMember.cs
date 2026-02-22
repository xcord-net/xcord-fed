namespace Xcord.Entities;

/// <summary>
/// Represents a DM channel membership (junction table with composite PK).
/// </summary>
public sealed class DmChannelMember
{
    /// <summary>
    /// User ID (part of composite PK, FK to User).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// DM Channel ID (part of composite PK, FK to DmChannel).
    /// </summary>
    public long DmChannelId { get; set; }

    /// <summary>
    /// Timestamp when the user joined the DM channel.
    /// </summary>
    public DateTimeOffset JoinedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public DmChannel DmChannel { get; set; } = null!;
}
