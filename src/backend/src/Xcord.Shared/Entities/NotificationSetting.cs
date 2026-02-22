using Xcord.Entities;

namespace Xcord.Entities;

/// <summary>
/// Represents notification preferences for a user at global, server, or channel scope.
/// Resolution order: Channel > Server > Global (most specific wins).
/// </summary>
public sealed class NotificationSetting
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// User ID (FK to User, Cascade delete).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Server ID (FK to Server, nullable).
    /// Null = global setting.
    /// </summary>
    public long? ServerId { get; set; }

    /// <summary>
    /// Channel ID (FK to Channel, nullable).
    /// Null = server-level or global.
    /// </summary>
    public long? ChannelId { get; set; }

    /// <summary>
    /// Notification level.
    /// </summary>
    public NotificationLevel Level { get; set; } = NotificationLevel.All;

    /// <summary>
    /// Suppress @everyone mentions.
    /// </summary>
    public bool SuppressEveryone { get; set; } = false;

    /// <summary>
    /// Suppress @role mentions.
    /// </summary>
    public bool SuppressRoles { get; set; } = false;

    /// <summary>
    /// Temporary mute expiry (null = not muted).
    /// </summary>
    public DateTimeOffset? MuteUntil { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Server? Server { get; set; }
    public Channel? Channel { get; set; }
}
