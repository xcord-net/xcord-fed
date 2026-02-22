using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string SmtpHost { get; set; } = string.Empty;

    [Required]
    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    public string SmtpUsername { get; set; } = string.Empty;

    public string SmtpPassword { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    [Required]
    public string FromName { get; set; } = string.Empty;

    public bool UseSsl { get; set; } = true;

    [Required]
    public bool DevMode { get; set; }
}
