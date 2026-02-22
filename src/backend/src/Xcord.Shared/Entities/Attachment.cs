namespace Xcord.Entities;

/// <summary>
/// Represents a file attachment linked to a message.
/// </summary>
public sealed class Attachment : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Message ID (FK to Message, Cascade delete). Nullable for pre-message uploads.
    /// </summary>
    public long? MessageId { get; set; }

    /// <summary>
    /// Original filename (max 256 characters).
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME content type (max 128 characters).
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// S3 object key (max 512 characters).
    /// </summary>
    public string S3Key { get; set; } = string.Empty;

    /// <summary>
    /// Image/video width in pixels (nullable).
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Image/video height in pixels (nullable).
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// S3 key for thumbnail (nullable). Populated asynchronously by ThumbnailProcessor
    /// after the upload is confirmed. Empty string means "not applicable" (non-image attachment).
    /// </summary>
    public string? ThumbnailS3Key { get; set; }

    /// <summary>
    /// Whether the upload has been confirmed by the client.
    /// </summary>
    public bool IsConfirmed { get; set; }

    /// <summary>
    /// Attachment creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Message? Message { get; set; }
}
