using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a thread (either a regular thread created from a message, or a forum post).
/// Threads have their own Conversation for message storage.
/// </summary>
public sealed class Thread : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Conversation ID (FK to Conversation, required).
    /// This is the conversation where thread messages are stored.
    /// </summary>
    public long ConversationId { get; set; }

    /// <summary>
    /// Channel ID (FK to Channel, required).
    /// The parent channel this thread belongs to.
    /// </summary>
    public long ChannelId { get; set; }

    /// <summary>
    /// Parent message ID (FK to Message, optional).
    /// Null for forum posts (standalone threads).
    /// Set for regular threads (created from a message).
    /// </summary>
    public long? ParentMessageId { get; set; }

    /// <summary>
    /// Thread title (required for forum posts, optional for regular threads).
    /// Max 100 characters.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Whether this thread is archived (default false).
    /// Archived threads are read-only unless manually unarchived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Whether this thread is locked (default false).
    /// Locked threads can only be posted in by moderators.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Auto-archive duration in minutes (default 1440 = 24h).
    /// Valid values: 60, 1440, 4320, 10080.
    /// </summary>
    public int AutoArchiveDurationMinutes { get; set; }

    /// <summary>
    /// Last activity timestamp (message posted, thread created, etc.).
    /// Used to determine when to auto-archive.
    /// </summary>
    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>
    /// Denormalized message count.
    /// Updated in the same transaction as message creation.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// Thread creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public Channel Channel { get; set; } = null!;
    public Message? ParentMessage { get; set; }
    public ICollection<ThreadMember> ThreadMembers { get; set; } = new List<ThreadMember>();
}
