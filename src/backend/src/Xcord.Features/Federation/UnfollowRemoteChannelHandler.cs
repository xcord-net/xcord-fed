using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Federation;

public sealed record UnfollowRemoteChannelRequest(long FollowId);

public sealed record UnfollowRemoteChannelResponse(bool Deleted);

public sealed class UnfollowRemoteChannelHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<UnfollowRemoteChannelHandler> logger)
    : IRequestHandler<UnfollowRemoteChannelRequest, Result<UnfollowRemoteChannelResponse>>
{
    public async Task<Result<UnfollowRemoteChannelResponse>> Handle(UnfollowRemoteChannelRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        var follow = await dbContext.FederationFollows
            .FirstOrDefaultAsync(f => f.Id == request.FollowId, cancellationToken);

        if (follow == null)
        {
            return Error.NotFound("FOLLOW_NOT_FOUND", "Federation follow not found");
        }

        // Only the user who created the follow or a server admin can unfollow
        if (follow.FollowedByUserId != userId)
        {
            return Error.Forbidden("FORBIDDEN", "You can only unfollow your own federation follows");
        }

        follow.DeletedAt = DateTimeOffset.UtcNow;
        follow.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} unfollowed federation follow {FollowId}", userId, request.FollowId);

        return new UnfollowRemoteChannelResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/federation/follows/{followId:long}", async (
            long followId,
            [FromServices] UnfollowRemoteChannelHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new UnfollowRemoteChannelRequest(followId), ct);
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("UnfollowRemoteChannel")
        .WithTags("Federation");
}
