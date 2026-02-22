using Xcord.Entities;

namespace Xcord.Entities;

/// <summary>
/// Represents a message in a conversation (channel, DM, thread).
/// </summary>
public sealed class Message : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Conversation ID (FK to Conversation, Cascade delete).
    /// </summary>
    public long ConversationId { get; set; }

    /// <summary>
    /// Author user ID (FK to User, SetNull on delete).
    /// Null for system messages (e.g., MemberJoin).
    /// </summary>
    public long? AuthorId { get; set; }

    /// <summary>
    /// Message type (Default, MemberJoin, etc.).
    /// </summary>
    public MessageType Type { get; set; }

    /// <summary>
    /// Message content (max 4000 characters).
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata (stored as jsonb).
    /// Used for storing URLs, embed data, poll info, etc.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Reply to message ID (self-reference, SetNull on delete).
    /// </summary>
    public long? ReplyToId { get; set; }

    /// <summary>
    /// Whether this message is pinned.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Timestamp when the message was pinned (null if not pinned).
    /// </summary>
    public DateTimeOffset? PinnedAt { get; set; }

    /// <summary>
    /// Whether embeds have been processed for this message.
    /// </summary>
    public bool EmbedsProcessed { get; set; }

    /// <summary>
    /// Last edit timestamp (null if never edited).
    /// </summary>
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>
    /// Message creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public User? Author { get; set; }
    public Message? ReplyTo { get; set; }
    public ICollection<Mention> Mentions { get; set; } = new List<Mention>();
    public ICollection<Embed> Embeds { get; set; } = new List<Embed>();
    public ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
