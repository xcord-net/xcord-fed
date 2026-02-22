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
    long? Before = null
);

public sealed class ListDmsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<ListDmsRequest, Result<DmChannelDto[]>>
{
    public async Task<Result<DmChannelDto[]>> Handle(ListDmsRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var currentUserId = userIdResult.Value;

        var limit = Math.Clamp(request.Limit, 1, 100);

        // Build query for DM channels the current user is a member of
        var query = dbContext.DmChannels
            .Where(dm => dm.Members.Any(m => m.UserId == currentUserId));

        if (request.Before.HasValue)
        {
            query = query.Where(dm => dm.Id < request.Before.Value);
        }

        // Get DM channels with members, ordered by most recent first
        var dmChannels = await query
            .Include(dm => dm.Members)
                .ThenInclude(m => m.User)
            .OrderByDescending(dm => dm.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var result = new List<DmChannelDto>();

        foreach (var dm in dmChannels)
        {
            // Get last message preview for this conversation
            var lastMessage = await dbContext.Messages
                .Where(m => m.ConversationId == dm.ConversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new MessagePreviewDto(
                    m.Id,
                    m.AuthorId,
                    m.Content ?? "",
                    m.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken);

            var members = dm.Members
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

        return result.ToArray();
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/@me/dms", async (
            [FromServices] ListDmsHandler handler,
            int? limit,
            long? before,
            CancellationToken ct) =>
            await handler.ExecuteAsync(
                new ListDmsRequest(
                    Limit: limit ?? 50,
                    Before: before),
                ct))
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("ListDms")
            .WithTags("DMs");
}
