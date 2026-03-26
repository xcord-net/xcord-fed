using Xcord.Entities;

namespace Xcord.Infrastructure.Services.Discord;

/// <summary>
/// Maps Discord permission bits to Xcord <see cref="Role"/> flags.
/// </summary>
public static class DiscordPermissionMapper
{
    // Discord permission bit constants
    private const long D_CREATE_INSTANT_INVITE       = 1L << 0;
    private const long D_KICK_MEMBERS                = 1L << 1;
    private const long D_BAN_MEMBERS                 = 1L << 2;
    private const long D_ADMINISTRATOR               = 1L << 3;
    private const long D_MANAGE_CHANNELS             = 1L << 4;
    private const long D_MANAGE_GUILD                = 1L << 5;
    private const long D_ADD_REACTIONS               = 1L << 6;
    private const long D_VIEW_CHANNEL                = 1L << 10;
    private const long D_SEND_MESSAGES               = 1L << 11;
    private const long D_MANAGE_MESSAGES             = 1L << 13;
    private const long D_EMBED_LINKS                 = 1L << 14;
    private const long D_ATTACH_FILES                = 1L << 15;
    private const long D_READ_MESSAGE_HISTORY        = 1L << 16;
    private const long D_MENTION_EVERYONE            = 1L << 17;
    private const long D_USE_EXTERNAL_EMOJIS         = 1L << 18;
    private const long D_CONNECT                     = 1L << 20;
    private const long D_SPEAK                       = 1L << 21;
    private const long D_MUTE_MEMBERS                = 1L << 22;
    private const long D_DEAFEN_MEMBERS              = 1L << 23;
    private const long D_MOVE_MEMBERS                = 1L << 24;
    private const long D_CHANGE_NICKNAME             = 1L << 26;
    private const long D_MANAGE_NICKNAMES            = 1L << 27;
    private const long D_MANAGE_ROLES                = 1L << 28;
    private const long D_MANAGE_WEBHOOKS             = 1L << 29;
    private const long D_MANAGE_EMOJIS_AND_STICKERS  = 1L << 30;
    private const long D_MANAGE_EVENTS               = 1L << 33;
    private const long D_CREATE_PUBLIC_THREADS       = 1L << 35;
    private const long D_CREATE_PRIVATE_THREADS      = 1L << 36;
    private const long D_SEND_MESSAGES_IN_THREADS    = 1L << 38;
    private const long D_MODERATE_MEMBERS            = 1L << 40;

    /// <summary>
    /// Maps a Discord permission bitmask to the closest equivalent Xcord <see cref="Role"/> bitmask.
    /// </summary>
    public static long MapPermissions(long discordPermissions)
    {
        long xcordRoles = 0;

        bool Has(long bit) => (discordPermissions & bit) != 0;
        void Add(Role role) => xcordRoles |= (long)role;

        if (Has(D_ADMINISTRATOR))               Add(Role.Administrator);
        if (Has(D_VIEW_CHANNEL))                Add(Role.ViewChannels);
        if (Has(D_MANAGE_GUILD))                Add(Role.ManageServer);
        if (Has(D_MANAGE_ROLES))                Add(Role.ManageGroups);
        if (Has(D_MANAGE_CHANNELS))             Add(Role.ManageChannels);
        if (Has(D_KICK_MEMBERS))                Add(Role.KickMembers);
        if (Has(D_BAN_MEMBERS))                 Add(Role.BanMembers);
        if (Has(D_CREATE_INSTANT_INVITE))       Add(Role.CreateInvite);
        if (Has(D_MANAGE_MESSAGES))             Add(Role.ManageMessages);
        if (Has(D_SEND_MESSAGES))               Add(Role.SendMessages);
        if (Has(D_EMBED_LINKS))                 Add(Role.EmbedLinks);
        if (Has(D_ATTACH_FILES))                Add(Role.AttachFiles);
        if (Has(D_READ_MESSAGE_HISTORY))        Add(Role.ReadMessageHistory);
        if (Has(D_USE_EXTERNAL_EMOJIS))         Add(Role.UseExternalEmojis);
        if (Has(D_CONNECT))                     Add(Role.Connect);
        if (Has(D_SPEAK))                       Add(Role.Speak);
        if (Has(D_MUTE_MEMBERS))                Add(Role.MuteMembers);
        if (Has(D_DEAFEN_MEMBERS))              Add(Role.DeafenMembers);
        if (Has(D_MOVE_MEMBERS))                Add(Role.MoveMembers);
        if (Has(D_SEND_MESSAGES_IN_THREADS))    Add(Role.SendMessagesInThreads);
        if (Has(D_CREATE_PUBLIC_THREADS))       Add(Role.CreatePublicThreads);
        if (Has(D_CREATE_PRIVATE_THREADS))      Add(Role.CreatePrivateThreads);
        if (Has(D_ADD_REACTIONS))               Add(Role.AddReactions);
        if (Has(D_MENTION_EVERYONE))            Add(Role.MentionEveryone);
        if (Has(D_CHANGE_NICKNAME))             Add(Role.ChangeNickname);
        if (Has(D_MANAGE_NICKNAMES))            Add(Role.ManageNicknames);
        if (Has(D_MANAGE_WEBHOOKS))             Add(Role.ManageWebhooks);
        if (Has(D_MANAGE_EMOJIS_AND_STICKERS))
        {
            Add(Role.ManageEmojis);
            Add(Role.ManageStickers);
        }
        if (Has(D_MANAGE_EVENTS))               Add(Role.ManageEvents);
        if (Has(D_MODERATE_MEMBERS))            Add(Role.TimeoutMembers);

        return xcordRoles;
    }
}
