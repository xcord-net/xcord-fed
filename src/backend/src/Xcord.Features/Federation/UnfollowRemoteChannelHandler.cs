using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Federation;

public sealed record UnfollowRemoteChannelRequest(long FollowId);

public sealed record UnfollowRemoteChannelResponse(bool Deleted);

public sealed class UnfollowRemoteChannelHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<UnfollowRemoteChannelHandler> logger)
    : IRequestHandler<UnfollowRemoteChannelRequest, Result<UnfollowRemoteChannelResponse>>
{
    public async Task<Result<UnfollowRemoteChannelResponse>> Handle(UnfollowRemoteChannelRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

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

        follow.SoftDelete();
        follow.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User {UserId} unfollowed federation follow {FollowId}", userId, request.FollowId);

        return new UnfollowRemoteChannelResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/federation/follows/{followId:long}", async (
            long followId,
            [FromServices] UnfollowRemoteChannelHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new UnfollowRemoteChannelRequest(followId), ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("UnfollowRemoteChannel")
        .WithTags("Federation");
}
