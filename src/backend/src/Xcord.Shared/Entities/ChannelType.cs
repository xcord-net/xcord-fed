namespace Xcord.Entities;

/// <summary>
/// Type of channel in a server.
/// </summary>
public enum ChannelType
{
    /// <summary>
    /// Text channel for messaging.
    /// </summary>
    Text = 0,

    /// <summary>
    /// Voice channel for audio/video communication.
    /// </summary>
    Voice = 1,

    /// <summary>
    /// Announcement channel (read-only for non-moderators).
    /// </summary>
    Announcement = 2,

    /// <summary>
    /// Forum channel (post-based with threads).
    /// </summary>
    Forum = 3
}
