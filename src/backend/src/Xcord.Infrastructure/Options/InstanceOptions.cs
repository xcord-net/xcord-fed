using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class InstanceOptions
{
    public const string SectionName = "Instance";

    [Required]
    public string Domain { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;
}
