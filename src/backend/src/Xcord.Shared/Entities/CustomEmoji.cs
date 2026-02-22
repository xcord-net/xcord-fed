using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a custom emoji uploaded to a server.
/// </summary>
public sealed class CustomEmoji : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Server ID that owns this emoji (FK to Server).
    /// </summary>
    public long ServerId { get; set; }

    /// <summary>
    /// Emoji name (max 32 characters, alphanumeric + underscores).
    /// Must be unique within the server.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL to the emoji image (S3/MinIO).
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// S3 object key for the emoji image.
    /// </summary>
    public string S3Key { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is an animated emoji (GIF).
    /// </summary>
    public bool IsAnimated { get; set; }

    /// <summary>
    /// User ID of the creator (FK to User).
    /// </summary>
    public long CreatorId { get; set; }

    /// <summary>
    /// Emoji creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Server Server { get; set; } = null!;
    public User Creator { get; set; } = null!;
}
