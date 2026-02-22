using Xcord.Entities;

namespace Xcord.Entities;

/// <summary>
/// Represents a permission override for a specific channel.
/// Can target either a role or a user.
/// Deny permissions take precedence over allow at the same level.
/// </summary>
public sealed class ChannelPermissionOverride
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Channel ID (FK will be set up when Channel entity exists).
    /// </summary>
    public long ChannelId { get; set; }

    /// <summary>
    /// Type of target (Role or User).
    /// </summary>
    public OverrideTargetType TargetType { get; set; }

    /// <summary>
    /// Target ID (RoleId or UserId - polymorphic, no FK constraint).
    /// </summary>
    public long TargetId { get; set; }

    /// <summary>
    /// Permission bits to explicitly allow.
    /// </summary>
    public long Allow { get; set; }

    /// <summary>
    /// Permission bits to explicitly deny.
    /// </summary>
    public long Deny { get; set; }

    // Navigation properties
    public Channel Channel { get; set; } = null!;
}
