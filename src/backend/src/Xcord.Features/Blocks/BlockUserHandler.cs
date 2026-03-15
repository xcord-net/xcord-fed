using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Blocks;

public sealed record BlockUserRequest(
    long UserId
);

public sealed class BlockUserHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions,
    INotificationService notificationService,
    ILogger<BlockUserHandler> logger) : IRequestHandler<BlockUserRequest, Result<UserBlockDto>>
{
    private readonly RedisOptions _redisOptions = redisOptions.Value;

    public async Task<Result<UserBlockDto>> Handle(BlockUserRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Cannot block self
        if (request.UserId == userId)
        {
            return Error.Validation("CANNOT_BLOCK_SELF", "You cannot block yourself");
        }

        // Check if target user exists
        var targetUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (targetUser == null)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        // Check if block already exists
        var existingBlock = await dbContext.UserBlocks
            .FirstOrDefaultAsync(ub => ub.BlockerId == userId && ub.BlockedId == request.UserId, cancellationToken);

        if (existingBlock != null)
        {
            return Error.Conflict("ALREADY_BLOCKED", "User is already blocked");
        }

        // Create user block
        var userBlock = new UserBlock
        {
            BlockerId = userId,
            BlockedId = request.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.UserBlocks.Add(userBlock);

        // Soft delete any existing friendships
        var friendships = await dbContext.Friendships
            .Where(f =>
                (f.SenderId == userId && f.ReceiverId == request.UserId) ||
                (f.SenderId == request.UserId && f.ReceiverId == userId))
            .ToListAsync(cancellationToken);

        foreach (var friendship in friendships)
        {
            friendship.SoftDelete();
        }

        // Remove blocked user from any shared 1:1 DM
        var dmChannels = await dbContext.DmChannels
            .Include(dm => dm.Members)
            .Where(dm => dm.IsGroup == false &&
                dm.Members.Any(m => m.UserId == userId) &&
                dm.Members.Any(m => m.UserId == request.UserId))
            .ToListAsync(cancellationToken);

        foreach (var dmChannel in dmChannels)
        {
            var memberToRemove = dmChannel.Members.FirstOrDefault(m => m.UserId == request.UserId);
            if (memberToRemove != null)
            {
                dbContext.DmChannelMembers.Remove(memberToRemove);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Notify the blocked user directly after save
        await notificationService.NotifyUserAsync(request.UserId, "Notify_UserBlocked", new
        {
            blockerId = userId,
            blockedId = request.UserId
        });

        // Cache block in Redis
        var db = redis.GetDatabase();
        var redisKey = $"{_redisOptions.ChannelPrefix}:blocks:{userId}";
        await db.SetAddAsync(redisKey, request.UserId);

        logger.LogInformation(
            "User {UserId} blocked user {BlockedUserId}",
            userId, request.UserId);

        // Return DTO
        return new UserBlockDto(
            userId,
            request.UserId,
            targetUser.Username,
            targetUser.DisplayName,
            targetUser.AvatarUrl,
            userBlock.CreatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/users/@me/blocks/{userId:long}", async (
            long userId,
            [FromServices] BlockUserHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new BlockUserRequest(userId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("BlockUser")
        .WithTags("Blocks");
}
