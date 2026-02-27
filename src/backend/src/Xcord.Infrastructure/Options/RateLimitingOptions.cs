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

    /// <summary>Max registrations per minute per IP (default 3).</summary>
    public int AuthRegisterPermitLimit { get; set; } = 3;

    /// <summary>Max password-reset requests per minute per IP (default 3).</summary>
    public int AuthForgotPasswordPermitLimit { get; set; } = 3;
}
