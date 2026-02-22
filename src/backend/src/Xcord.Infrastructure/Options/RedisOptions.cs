using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string ChannelPrefix { get; set; } = string.Empty;
}
