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
    Forum = 3,

    /// <summary>
    /// Stage channel: a broadcast with a host-controlled stage, optionally
    /// relayed to external platforms.
    /// </summary>
    /// <remarks>
    /// Until this existed the Streaming capability could only be set by passing
    /// Capabilities explicitly, which nothing in the product did - so a stage
    /// channel could not actually be created through the UI, and the integration
    /// test wrote one straight to the database to get around it.
    /// </remarks>
    Stage = 4
}
