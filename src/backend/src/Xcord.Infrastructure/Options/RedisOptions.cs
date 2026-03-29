using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string ChannelPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Optional Redis ACL username provisioned by the hub. When set, the instance
    /// authenticates with this user which is restricted to its own key namespace.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Optional Redis ACL password for the per-instance user.
    /// </summary>
    public string? Password { get; set; }
}
