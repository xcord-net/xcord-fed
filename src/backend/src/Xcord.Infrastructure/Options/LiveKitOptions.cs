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

    /// <summary>
    /// Base URL of the LiveKit egress service (null disables egress).
    /// </summary>
    public string? EgressServiceUrl { get; set; }

    /// <summary>
    /// Base URL passed to the egress compositor for rendering the broadcast template
    /// (e.g., https://instance.example.com). This should resolve to the backend public URL.
    /// </summary>
    public string? EgressTemplateBaseUrl { get; set; }

    /// <summary>
    /// Public URL prefix for HLS output (e.g., https://instance.example.com/hls).
    /// </summary>
    public string? HlsBaseUrl { get; set; }

    /// <summary>
    /// Shared secret used to validate egress webhook signatures.
    /// </summary>
    public string? EgressWebhookSecret { get; set; }
}
