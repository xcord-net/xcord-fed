using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class SnowflakeOptions
{
    public const string SectionName = "Snowflake";

    [Required]
    [Range(0, 1023)]
    public int WorkerId { get; set; }

    [Required]
    public long Epoch { get; set; }
}
