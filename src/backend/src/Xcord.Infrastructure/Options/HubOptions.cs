using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class HubOptions
{
    public const string SectionName = "Hub";

    [Required]
    public bool Enabled { get; set; }

    public string? GatewayApiUrl { get; set; }

    public string? BootstrapToken { get; set; }

    public string? FederationToken { get; set; }

    public string? Origin { get; set; }
}
