namespace Xcord.Entities;

/// <summary>
/// Permission bitfield flags (64-bit).
/// Bit layout is shared between the backend and the RoleManager UI to ensure
/// roles created via the UI have the correct backend-enforced permissions.
/// </summary>
[Flags]
public enum Permission : long
{
    // General / administrative permissions (bits 0–6)
    // Note: ViewChannels (bit 0) doubles as the front-end "Administrator" checkbox
    // in the PERMISSION_FLAGS table; the Administrator super-permission is at bit 62.
    ViewChannels = 1L << 0,      // frontend: Administrator checkbox (bit 0)
    ManageServer = 1L << 1,
    ManageRoles = 1L << 2,
    ManageChannels = 1L << 3,
    KickMembers = 1L << 4,
    BanMembers = 1L << 5,
    CreateInvite = 1L << 6,

    // Messaging permissions — bit positions MUST match the frontend RoleManager PERMISSION_FLAGS
    ManageMessages = 1L << 7,   // frontend bit 7
    SendMessages = 1L << 8,     // frontend bit 8
    EmbedLinks = 1L << 9,       // frontend bit 9
    AttachFiles = 1L << 10,     // frontend bit 10
    ReadMessageHistory = 1L << 11, // frontend bit 11

    // Extended permissions (bits 12–26)
    UseExternalEmojis = 1L << 12,
    Connect = 1L << 13,
    Speak = 1L << 14,
    MuteMembers = 1L << 15,
    DeafenMembers = 1L << 16,
    MoveMembers = 1L << 17,
    Video = 1L << 18,
    ShareScreen = 1L << 19,
    SendMessagesInThreads = 1L << 20,
    CreatePublicThreads = 1L << 21,
    CreatePrivateThreads = 1L << 22,
    AddReactions = 1L << 23,
    MentionEveryone = 1L << 24,
    ChangeNickname = 1L << 25,
    ManageNicknames = 1L << 26,

    // Moderation permissions (bits 27–33)
    TimeoutMembers = 1L << 27,
    ManageEmojis = 1L << 28,
    ManageStickers = 1L << 29,
    ManageWebhooks = 1L << 30,
    ManageEvents = 1L << 31,
    CreatePolls = 1L << 32,
    ManageReports = 1L << 33,
    ManageAutomod = 1L << 34,

    // Administrator super-permission (bit 62)
    Administrator = 1L << 62
}
