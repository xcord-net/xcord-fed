using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Friends;

public sealed record AcceptFriendRequestRequest(
    long FriendshipId
);

public sealed class AcceptFriendRequestHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IOutboxWriter outboxWriter,
    ILogger<AcceptFriendRequestHandler> logger) : IRequestHandler<AcceptFriendRequestRequest, Result<FriendshipDto>>
{
    public async Task<Result<FriendshipDto>> Handle(AcceptFriendRequestRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Find friendship with user info
        var friendship = await dbContext.Friendships
            .Include(f => f.Sender)
            .Include(f => f.Receiver)
            .FirstOrDefaultAsync(f => f.Id == request.FriendshipId, cancellationToken);

        if (friendship == null)
        {
            return Error.NotFound("FRIENDSHIP_NOT_FOUND", "Friend request not found");
        }

        // Verify current user is the receiver
        if (friendship.ReceiverId != userId)
        {
            return Error.Forbidden("NOT_RECEIVER", "You can only accept friend requests sent to you");
        }

        // Verify status is pending
        if (friendship.Status != FriendshipStatus.Pending)
        {
            return Error.Validation("INVALID_STATUS", "Friend request is not pending");
        }

        // Update status to Accepted
        friendship.Status = FriendshipStatus.Accepted;

        // Write outbox event
        await outboxWriter.WriteAsync(
            dbContext,
            "Friend.Accepted",
            new
            {
                friendshipId = friendship.Id,
                senderId = friendship.SenderId,
                receiverId = friendship.ReceiverId
            },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} accepted friend request from user {SenderId}",
            userId, friendship.SenderId);

        // Return DTO
        return new FriendshipDto(
            friendship.Id,
            friendship.SenderId,
            friendship.Sender.Username,
            friendship.Sender.DisplayName,
            friendship.Sender.AvatarUrl,
            friendship.ReceiverId,
            friendship.Receiver.Username,
            friendship.Receiver.DisplayName,
            friendship.Receiver.AvatarUrl,
            FriendshipStatus.Accepted,
            friendship.CreatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/users/@me/friends/{id:long}/accept", async (
            long id,
            [FromServices] AcceptFriendRequestHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new AcceptFriendRequestRequest(id), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("AcceptFriendRequest")
        .WithTags("Friends");
}
