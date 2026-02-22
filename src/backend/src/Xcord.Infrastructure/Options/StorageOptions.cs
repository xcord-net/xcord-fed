using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string Bucket { get; set; } = string.Empty;

    [Required]
    public string AccessKey { get; set; } = string.Empty;

    [Required]
    public string SecretKey { get; set; } = string.Empty;
}
