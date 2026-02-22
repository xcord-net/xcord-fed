using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class GifOptions
{
    public const string SectionName = "Gif";

    [Required]
    [RegularExpression("^(tenor|giphy|none)$")]
    public string Provider { get; set; } = "none";

    public string? ApiKey { get; set; }

    public string? ApiUrl { get; set; }
}
