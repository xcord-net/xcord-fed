using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string ApiSecret { get; set; } = string.Empty;
}
