using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Deck;

/// <summary>
/// One call that returns everything the Deck shell needs to render: the user's
/// full map of communities and conversations, their unread and mention state,
/// and which voice rooms are live right now.
///
/// The Deck has no sidebar to lazily fill as you click around, so the client
/// cannot discover conversations one server at a time any more. The switchboard
/// is the only full map and it has to be complete the moment it opens, which is
/// why this is one aggregate rather than a fan-out of per-server calls.
/// </summary>
public sealed record GetDeckRequest;

public sealed record DeckConversationDto(
    long Id,
    long ConversationId,
    string Name,
    string Kind,
    int Unread,
    int Mentions,
    int LiveVoiceCount
);

public sealed record DeckCommunityDto(
    long Id,
    string Name,
    string? IconUrl,
    List<DeckConversationDto> Conversations
);

public sealed record GetDeckResponse(
    List<DeckCommunityDto> Communities,
    List<DeckConversationDto> DirectMessages,
    int TotalUnread,
    int TotalMentions
);

public sealed class GetDeckHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService)
    : IRequestHandler<GetDeckRequest, Result<GetDeckResponse>>
{
    /// <summary>
    /// Ceiling on conversations returned. The switchboard is searchable, so a
    /// user with an enormous map still gets a usable list, and the query stays
    /// bounded rather than degrading with account age.
    /// </summary>
    private const int MaxConversations = 2000;

    public async Task<Result<GetDeckResponse>> Handle(GetDeckRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Communities the user is actually a member of. Membership is the
        // authorization boundary for everything below it, and it is expressed
        // as a subquery rather than a list of ids fetched first: this is the
        // shell's one bootstrap call, so every round trip it makes is one the
        // user waits through before anything renders. Reading the ids only to
        // send them straight back as parameters cost a full round trip and grew
        // the parameter list with the account.
        var servers = await dbContext.Servers
            .AsNoTracking()
            .Where(s => dbContext.ServerMembers.Any(m => m.UserId == userId && m.ServerId == s.Id))
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.IconUrl })
            .ToListAsync(cancellationToken);

        var channels = await dbContext.Channels
            .AsNoTracking()
            .Where(c => dbContext.ServerMembers.Any(m => m.UserId == userId && m.ServerId == c.ServerId))
            .OrderBy(c => c.ServerId).ThenBy(c => c.Position).ThenBy(c => c.Name)
            .Take(MaxConversations)
            .Select(c => new { c.Id, c.ConversationId, c.ServerId, c.Name, c.Type })
            .ToListAsync(cancellationToken);

        // Membership gets you into a community; it does not get you into every
        // room in it. Without this second check a channel restricted to a group
        // still appeared in the switchboard for everyone - and the switchboard is
        // the app's complete map, so that is where such a leak is most visible.
        // One lookup per community rather than per channel.
        var visibleChannels = new List<(long Id, long ConversationId, long ServerId, string Name, ChannelType Type)>();
        foreach (var group in channels.GroupBy(c => c.ServerId))
        {
            var roles = await roleService
                .GetChannelRolesForServer(userId, group.Key, group.Select(c => c.Id).ToList())
                .ConfigureAwait(false);
            foreach (var c in group)
            {
                if ((roles.GetValueOrDefault(c.Id) & (long)Role.ViewChannels) == 0) continue;
                visibleChannels.Add((c.Id, c.ConversationId, c.ServerId, c.Name, c.Type));
            }
        }
        channels = visibleChannels
            .Select(c => new { c.Id, c.ConversationId, c.ServerId, c.Name, c.Type })
            .ToList();

        // Read state is keyed by conversation, so one lookup covers channels
        // and DMs alike.
        var readStates = await dbContext.ReadStates
            .AsNoTracking()
            .Where(rs => rs.UserId == userId)
            .Select(rs => new { rs.ConversationId, rs.UnreadCount, rs.MentionCount })
            .ToListAsync(cancellationToken);

        var unreadByConversation = readStates.ToDictionary(
            rs => rs.ConversationId,
            rs => (Unread: rs.UnreadCount, Mentions: rs.MentionCount));

        // "Live now" is simply someone being in the room. Counting occupants
        // per voice channel is what lets Home say who is in there.
        var channelIds = channels.Select(c => c.Id).ToList();
        var liveVoice = await dbContext.VoiceStates
            .AsNoTracking()
            .Where(vs => channelIds.Contains(vs.ChannelId))
            .GroupBy(vs => vs.ChannelId)
            .Select(g => new { ChannelId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var liveByChannel = liveVoice.ToDictionary(v => v.ChannelId, v => v.Count);

        DeckConversationDto ToDto(long id, long conversationId, string name, string kind)
        {
            var state = unreadByConversation.TryGetValue(conversationId, out var s)
                ? s
                : (Unread: 0, Mentions: 0);
            liveByChannel.TryGetValue(id, out var live);
            return new DeckConversationDto(id, conversationId, name, kind, state.Unread, state.Mentions, live);
        }

        var communities = servers
            .Select(s => new DeckCommunityDto(
                s.Id,
                s.Name,
                s.IconUrl,
                channels
                    .Where(c => c.ServerId == s.Id)
                    .Select(c => ToDto(c.Id, c.ConversationId, c.Name, c.Type.ToString()))
                    .ToList()))
            .ToList();

        var dmChannels = await dbContext.DmChannels
            .AsNoTracking()
            .Where(d => dbContext.DmChannelMembers.Any(m => m.UserId == userId && m.DmChannelId == d.Id))
            .Select(d => new { d.Id, d.ConversationId, d.Name })
            .ToListAsync(cancellationToken);

        var directMessages = dmChannels
            .Select(d => ToDto(d.Id, d.ConversationId, d.Name ?? "Direct message", "Dm"))
            .ToList();

        // Totals come from the user's whole read state, not just the rows that
        // matched a conversation above: the Home badge must not silently drop
        // unread that lives outside the current page of conversations.
        var totalUnread = readStates.Sum(rs => rs.UnreadCount);
        var totalMentions = readStates.Sum(rs => rs.MentionCount);

        return new GetDeckResponse(communities, directMessages, totalUnread, totalMentions);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/deck", async (
            [FromServices] GetDeckHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetDeckRequest(), ct))
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("GetDeck")
            .WithTags("Deck");
}
