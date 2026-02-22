namespace Xcord.Entities;

/// <summary>
/// Type of conversation container.
/// </summary>
public enum ConversationType
{
    /// <summary>
    /// Server channel conversation.
    /// </summary>
    Channel = 0,

    /// <summary>
    /// Direct message (1:1 or group DM) conversation.
    /// </summary>
    DmChannel = 1,

    /// <summary>
    /// Thread conversation.
    /// </summary>
    Thread = 2
}
