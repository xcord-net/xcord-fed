using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a sticker image that can be sent in messages.
/// </summary>
public sealed class Sticker : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Sticker pack ID (FK to StickerPack).
    /// </summary>
    public long StickerPackId { get; set; }

    /// <summary>
    /// Sticker name (max 32 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated search tags (max 200 characters).
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// URL to the sticker image (S3/MinIO).
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// S3 object key for the sticker image.
    /// </summary>
    public string S3Key { get; set; } = string.Empty;

    /// <summary>
    /// Sticker creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public StickerPack StickerPack { get; set; } = null!;
}
