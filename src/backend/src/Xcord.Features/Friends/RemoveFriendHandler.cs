using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Friends;

public sealed record RemoveFriendRequest(
    long FriendshipId
);

public sealed class RemoveFriendHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<RemoveFriendHandler> logger) : IRequestHandler<RemoveFriendRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveFriendRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Find friendship
        var friendship = await dbContext.Friendships
            .FirstOrDefaultAsync(f => f.Id == request.FriendshipId, cancellationToken);

        if (friendship == null)
        {
            return Error.NotFound("FRIENDSHIP_NOT_FOUND", "Friendship not found");
        }

        // Verify current user is sender or receiver
        if (friendship.SenderId != userId && friendship.ReceiverId != userId)
        {
            return Error.Forbidden("NOT_PARTICIPANT", "You can only remove friendships you are part of");
        }

        // Soft delete the friendship
        friendship.SoftDelete();

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} removed friendship {FriendshipId}",
            userId, request.FriendshipId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/users/@me/friends/{id:long}", async (
            long id,
            [FromServices] RemoveFriendHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new RemoveFriendRequest(id), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("RemoveFriend")
        .WithTags("Friends");
}
