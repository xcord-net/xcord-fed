using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.ReadState;

public sealed record GetUnreadCountRequest;

public sealed record GetUnreadCountResponse(
    int TotalUnread,
    int TotalMentions
);

public sealed class GetUnreadCountHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<GetUnreadCountRequest, Result<GetUnreadCountResponse>>
{
    public async Task<Result<GetUnreadCountResponse>> Handle(GetUnreadCountRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Sum UnreadCount and MentionCount across all ReadState entries for this user
        var readStates = await dbContext.ReadStates
            .Where(rs => rs.UserId == userId)
            .ToListAsync(cancellationToken);

        var totalUnread = readStates.Sum(rs => rs.UnreadCount);
        var totalMentions = readStates.Sum(rs => rs.MentionCount);

        return new GetUnreadCountResponse(
            TotalUnread: totalUnread,
            TotalMentions: totalMentions
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/@me/unread-count", async (
            [FromServices] GetUnreadCountHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetUnreadCountRequest(), ct))
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("GetUnreadCount")
            .WithTags("ReadState");
}
