using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.ReadState;

public sealed record GetUnreadCountRequest;

public sealed record GetUnreadCountResponse(
    int TotalUnread,
    int TotalMentions
);

public sealed class GetUnreadCountHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor) : IRequestHandler<GetUnreadCountRequest, Result<GetUnreadCountResponse>>
{
    public async Task<Result<GetUnreadCountResponse>> Handle(GetUnreadCountRequest request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

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
