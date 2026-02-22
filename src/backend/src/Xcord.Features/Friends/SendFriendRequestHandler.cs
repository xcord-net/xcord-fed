using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Friends;

public sealed class SendFriendRequestHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    SnowflakeIdGenerator idGenerator,
    IOutboxWriter outboxWriter,
    ILogger<SendFriendRequestHandler> logger) : IRequestHandler<SendFriendRequestRequest, Result<FriendshipDto>>
{
    public async Task<Result<FriendshipDto>> Handle(SendFriendRequestRequest request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Cannot send friend request to self
        if (request.UserId == userId)
        {
            return Error.Validation("CANNOT_FRIEND_SELF", "You cannot send a friend request to yourself");
        }

        // Check if target user exists
        var targetUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (targetUser == null)
        {
            return Error.NotFound("USER_NOT_FOUND", "Target user not found");
        }

        // Check if either user has blocked the other
        var isBlocked = await dbContext.UserBlocks
            .AnyAsync(ub =>
                (ub.BlockerId == userId && ub.BlockedId == request.UserId) ||
                (ub.BlockerId == request.UserId && ub.BlockedId == userId),
                cancellationToken);

        if (isBlocked)
        {
            return Error.Validation("BLOCKED", "Cannot send friend request to this user");
        }

        // Check if friendship already exists (either direction)
        var existingFriendship = await dbContext.Friendships
            .FirstOrDefaultAsync(f =>
                (f.SenderId == userId && f.ReceiverId == request.UserId) ||
                (f.SenderId == request.UserId && f.ReceiverId == userId),
                cancellationToken);

        if (existingFriendship != null)
        {
            if (existingFriendship.Status == FriendshipStatus.Accepted)
            {
                return Error.Conflict("ALREADY_FRIENDS", "You are already friends with this user");
            }
            else
            {
                return Error.Conflict("REQUEST_ALREADY_SENT", "Friend request already exists");
            }
        }

        // Get current user for DTO
        var currentUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (currentUser == null)
        {
            return Error.NotFound("USER_NOT_FOUND", "Current user not found");
        }

        // Create friendship
        var friendship = new Friendship
        {
            Id = idGenerator.NextId(),
            SenderId = userId,
            ReceiverId = request.UserId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Friendships.Add(friendship);

        // Write outbox event
        await outboxWriter.WriteAsync(
            dbContext,
            "Friend.RequestSent",
            new
            {
                friendshipId = friendship.Id,
                senderId = userId,
                receiverId = request.UserId
            },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} sent friend request to user {TargetUserId}",
            userId, request.UserId);

        // Return DTO
        return new FriendshipDto(
            friendship.Id,
            userId,
            currentUser.Username,
            currentUser.DisplayName,
            currentUser.AvatarUrl,
            request.UserId,
            targetUser.Username,
            targetUser.DisplayName,
            targetUser.AvatarUrl,
            FriendshipStatus.Pending,
            friendship.CreatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/users/@me/friends", async (
            SendFriendRequestRequest request,
            IRequestHandler<SendFriendRequestRequest, Result<FriendshipDto>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(request, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("SendFriendRequest")
        .WithTags("Friends");
}
