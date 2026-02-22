namespace Xcord.Entities;

/// <summary>
/// Type of message (system messages for events vs user messages).
/// </summary>
public enum MessageType
{
    Default = 0,
    MemberJoin = 1,
    MemberLeave = 2,
    ChannelNameChange = 3,
    ChannelTopicChange = 4,
    PinnedMessage = 5,
    RoleCreated = 6,
    RoleDeleted = 7,
    ServerUpdated = 8,
    InviteCreated = 9,
    ThreadCreated = 10,
    ThreadArchived = 11,
    PollCreated = 12,
    ScheduledEventCreated = 13,
    CallStarted = 14,

    /// <summary>
    /// Voice message with audio recording attachment.
    /// </summary>
    VoiceMessage = 15,

    /// <summary>
    /// System message posted when a member is kicked from the server.
    /// </summary>
    MemberKick = 16,

    /// <summary>
    /// System message posted when a member is banned from the server.
    /// </summary>
    MemberBan = 17
}
