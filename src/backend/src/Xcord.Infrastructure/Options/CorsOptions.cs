using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    [Required]
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
