using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a direct message channel (1:1 or group).
/// Each DmChannel has a Conversation for message storage.
/// </summary>
public sealed class DmChannel : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to Conversation (required, unique).
    /// </summary>
    public long ConversationId { get; set; }

    /// <summary>
    /// Whether this is a group DM (false for 1:1 DMs).
    /// </summary>
    public bool IsGroup { get; set; }

    /// <summary>
    /// Group DM name (null for 1:1 DMs, max 100 characters).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Group DM owner (null for 1:1 DMs, FK to User).
    /// </summary>
    public long? OwnerId { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public User? Owner { get; set; }
    public ICollection<DmChannelMember> Members { get; set; } = new List<DmChannelMember>();
}
