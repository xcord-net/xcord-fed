using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a collection of stickers.
/// Can be server-specific or global (when ServerId is null).
/// </summary>
public sealed class StickerPack : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Server ID that owns this pack (FK to Server).
    /// Null indicates a global sticker pack.
    /// </summary>
    public long? ServerId { get; set; }

    /// <summary>
    /// Sticker pack name (max 100 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Sticker pack description (max 200 characters).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Pack creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Server? Server { get; set; }
    public ICollection<Sticker> Stickers { get; set; } = new List<Sticker>();
}
