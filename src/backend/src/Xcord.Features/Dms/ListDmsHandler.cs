using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Dms;

public sealed record ListDmsRequest(
    int Limit = 50,
    string? Cursor = null
);

public sealed record ListDmsResponse(
    DmChannelDto[] DmChannels,
    string? NextCursor = null
);

public sealed class ListDmsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ICursorService cursorService) : IRequestHandler<ListDmsRequest, Result<ListDmsResponse>>
{
    public async Task<Result<ListDmsResponse>> Handle(ListDmsRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var currentUserId = userIdResult.Value;

        // Decode opaque cursor (returns null when no cursor was supplied)
        var cursorResult = cursorService.Decode(request.Cursor);
        if (cursorResult.IsFailure) return cursorResult.Error;
        var beforeId = cursorResult.Value;

        var limit = Math.Clamp(request.Limit, 1, 100);

        // Build query for DM channels the current user is a member of
        var query = dbContext.DmChannels
            .Where(dm => dm.Members.Any(m => m.UserId == currentUserId));

        if (beforeId.HasValue)
        {
            query = query.Where(dm => dm.Id < beforeId.Value);
        }

        // Get DM channels with members, ordered by most recent first
        var dmChannels = await query
            .Include(dm => dm.Members)
                .ThenInclude(m => m.User)
            .OrderByDescending(dm => dm.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        // Resolve every DM's last-message preview with two set-based queries
        // instead of one query per DM. Snowflake IDs are time-ordered, so the
        // max ID per conversation is its most recent message.
        var conversationIds = dmChannels.Select(dm => dm.ConversationId).ToList();

        var latestMessageIds = await dbContext.Messages
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.Max(m => m.Id))
            .ToListAsync(cancellationToken);

        var lastMessagesByConversation = await dbContext.Messages
            .Where(m => latestMessageIds.Contains(m.Id))
            .Select(m => new
            {
                m.ConversationId,
                Preview = new MessagePreviewDto(m.Id, m.AuthorId, m.Content ?? "", m.CreatedAt)
            })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Preview, cancellationToken);

        var result = new List<DmChannelDto>();

        foreach (var dm in dmChannels)
        {
            var lastMessage = lastMessagesByConversation.GetValueOrDefault(dm.ConversationId);

            // Order members so the OTHER party is first for 1:1 DMs. This lets the
            // frontend identify the recipient as members[0] without needing to know
            // currentUserId synchronously (avoids a race where the user store hasn't
            // loaded yet and the recipient ends up being identified as self).
            var members = dm.Members
                .OrderBy(m => m.UserId == currentUserId ? 1 : 0)
                .Select(m => new DmMemberDto(
                    m.UserId,
                    m.User.Username,
                    m.User.DisplayName,
                    m.User.AvatarUrl,
                    m.JoinedAt))
                .ToArray();

            result.Add(new DmChannelDto(
                dm.Id,
                dm.ConversationId,
                dm.IsGroup,
                dm.Name,
                dm.OwnerId,
                members,
                lastMessage
            ));
        }

        var nextCursor = result.Count == limit && result.Count > 0
            ? cursorService.Encode(result[^1].Id)
            : null;

        return new ListDmsResponse(DmChannels: result.ToArray(), NextCursor: nextCursor);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/@me/dms", async (
            [FromServices] ListDmsHandler handler,
            int? limit,
            string? cursor,
            CancellationToken ct) =>
            await handler.ExecuteAsync(
                new ListDmsRequest(
                    Limit: limit ?? 50,
                    Cursor: cursor),
                ct))
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("ListDms")
            .WithTags("DMs");
}
