namespace Xcord.Entities;

/// <summary>
/// Represents a rich embed/link preview attached to a message.
/// </summary>
public sealed class Embed : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Message ID (FK to Message, Cascade delete).
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// Original URL that this embed represents (max 2048).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Embed title extracted from OpenGraph (max 256).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Embed description extracted from OpenGraph (max 4096).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Proxied image URL for the embed image (max 512).
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// S3 key for the proxied embed image (max 512).
    /// </summary>
    public string? ImageS3Key { get; set; }

    /// <summary>
    /// Site name extracted from OpenGraph (max 128).
    /// </summary>
    public string? SiteName { get; set; }

    /// <summary>
    /// Hex color for embed styling (max 7, e.g., #FF5733).
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Position/order of this embed within the message (0-based).
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Message Message { get; set; } = null!;
}
