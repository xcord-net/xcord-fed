using Xcord.Entities;

namespace Xcord.Entities;

/// <summary>
/// Represents a unified conversation container.
/// Channels, DmChannels, and Threads all reference a Conversation for message storage.
/// </summary>
public sealed class Conversation
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Type of conversation (Channel, DmChannel, Thread).
    /// </summary>
    public ConversationType Type { get; set; }
}
