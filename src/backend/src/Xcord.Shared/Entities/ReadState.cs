namespace Xcord.Entities;

/// <summary>
/// Represents read state tracking for a user in a conversation.
/// Composite PK: UserId + ConversationId.
/// NOT soft-deleted - hard-deleted when user leaves conversation.
/// Concurrency control: a PostgreSQL xmin system column (configured in
/// ReadStateConfiguration as a shadow property) acts as the optimistic
/// concurrency token. The recommended write path for UnreadCount/MentionCount
/// is an atomic ExecuteUpdateAsync (UPDATE ... SET UnreadCount = UnreadCount + 1)
/// which sidesteps the read-modify-write race entirely.
/// </summary>
public sealed class ReadState
{
    /// <summary>
    /// User ID (FK to User, part of composite PK).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Conversation ID (FK to Conversation, part of composite PK).
    /// </summary>
    public long ConversationId { get; set; }

    /// <summary>
    /// Last read message ID (FK to Message, nullable).
    /// Null if no messages have been read yet.
    /// </summary>
    public long? LastReadMessageId { get; set; }

    /// <summary>
    /// Number of unread messages in this conversation.
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// Number of unread mentions for this user in this conversation.
    /// </summary>
    public int MentionCount { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
    public Message? LastReadMessage { get; set; }
}
