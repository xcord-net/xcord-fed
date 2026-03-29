using Xcord;
using Xcord.Entities;

namespace Xcord.Entities;

/// <summary>
/// Represents a channel in a server.
/// All channels have a ConversationId for unified message storage.
/// </summary>
public sealed class Channel : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Conversation ID (FK to Conversation, Cascade delete, Unique).
    /// </summary>
    public long ConversationId { get; set; }

    /// <summary>
    /// Server ID (FK to Server, Cascade delete).
    /// </summary>
    public long ServerId { get; set; }

    /// <summary>
    /// Category ID (FK to Category, SetNull on delete).
    /// Null means the channel is not in a category (uncategorized).
    /// </summary>
    public long? CategoryId { get; set; }

    /// <summary>
    /// Channel name (max 100 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Channel topic/description (max 1024 characters).
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// Channel type (Text, Voice, Announcement, Forum).
    /// Preserved for backward compatibility in serialization.
    /// Derived from Capabilities when possible.
    /// </summary>
    public ChannelType Type { get; set; }

    /// <summary>
    /// Bitfield of channel capabilities (Chat, Voice, Video, Forum, Announcement).
    /// A channel can support multiple capabilities simultaneously.
    /// </summary>
    public ChannelCapability Capabilities { get; set; } = ChannelCapability.Chat;

    /// <summary>
    /// Optional group required for access. Null = everyone can access.
    /// </summary>
    public long? AccessGroupId { get; set; }

    /// <summary>
    /// Position for ordering within category (lower = higher up).
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Slow mode delay in seconds (null = disabled).
    /// </summary>
    public int? SlowModeSeconds { get; set; }

    /// <summary>
    /// Whether this channel is marked as NSFW.
    /// </summary>
    public bool IsNsfw { get; set; }

    /// <summary>
    /// Default sort order for forum channels (null for non-forum channels).
    /// </summary>
    public ForumSort? DefaultSortOrder { get; set; }

    /// <summary>
    /// Whether forum posts require at least one tag (forum channels only).
    /// </summary>
    public bool RequireTag { get; set; }

    /// <summary>
    /// Default auto-archive duration in minutes for new forum posts (forum channels only).
    /// </summary>
    public int? DefaultAutoArchiveDuration { get; set; }

    /// <summary>
    /// Channel creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public Server Server { get; set; } = null!;
    public Category? Category { get; set; }
    public Group? AccessGroup { get; set; }
    public ICollection<ChannelPermissionOverride> PermissionOverrides { get; set; } = new List<ChannelPermissionOverride>();
}
