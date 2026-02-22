using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Required]
    [Range(1, int.MaxValue)]
    public int MaxRequests { get; set; }

    [Required]
    [Range(1, 3600)]
    public int WindowSeconds { get; set; }
}
