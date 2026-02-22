using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a tag that can be applied to forum posts.
/// </summary>
public sealed class ForumTag : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Channel ID (FK to Channel, the forum channel).
    /// </summary>
    public long ChannelId { get; set; }

    /// <summary>
    /// Tag name (max 20 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional Unicode emoji.
    /// </summary>
    public string? EmojiUnicode { get; set; }

    /// <summary>
    /// Optional custom emoji ID (FK to custom emoji).
    /// </summary>
    public long? EmojiId { get; set; }

    /// <summary>
    /// Whether only moderators can apply this tag.
    /// </summary>
    public bool IsModerated { get; set; }

    /// <summary>
    /// Display order position.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Channel Channel { get; set; } = null!;
}
