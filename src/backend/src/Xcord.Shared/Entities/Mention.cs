namespace Xcord.Entities;

/// <summary>
/// Represents a user or role mention in a message.
/// Created during message processing pipeline.
/// </summary>
public sealed class Mention
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
    /// Mentioned user ID (FK to User, nullable).
    /// Null if this is a role or @everyone mention.
    /// </summary>
    public long? MentionedUserId { get; set; }

    /// <summary>
    /// Mentioned group ID (FK to Group, nullable).
    /// Null if this is a user or @everyone mention.
    /// </summary>
    public long? MentionedGroupId { get; set; }

    /// <summary>
    /// Whether this is an @everyone mention.
    /// </summary>
    public bool IsEveryone { get; set; }

    // Navigation properties
    public Message Message { get; set; } = null!;
    public User? MentionedUser { get; set; }
    public Group? MentionedGroup { get; set; }
}
