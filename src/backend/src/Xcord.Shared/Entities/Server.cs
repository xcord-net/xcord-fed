using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a server (guild) in the instance.
/// Servers are private by default - access is mediated via invites.
/// </summary>
public sealed class Server : ISoftDeletable
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string? BannerUrl { get; set; }
    public long OwnerId { get; set; }
    public int MemberCount { get; set; } = 0;
    public string? PreferredLocale { get; set; }

    /// <summary>
    /// Custom vanity invite slug (e.g., "my-server" for /invite/my-server).
    /// </summary>
    public string? VanitySlug { get; set; }

    /// <summary>
    /// Current boost level (0-3) based on boost count.
    /// </summary>
    public int BoostLevel { get; set; }

    /// <summary>
    /// Current number of active boosts.
    /// </summary>
    public int BoostCount { get; set; }

    /// <summary>
    /// The channel ID used for system messages (join/leave notifications).
    /// Null means system messages are disabled.
    /// </summary>
    public long? SystemChannelId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public User Owner { get; set; } = null!;
}
