using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Blocks;

public sealed record UnblockUserRequest(
    long UserId
);

public sealed class UnblockUserHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions,
    INotificationService notificationService,
    ILogger<UnblockUserHandler> logger) : IRequestHandler<UnblockUserRequest, Result<bool>>
{
    private readonly RedisOptions _redisOptions = redisOptions.Value;

    public async Task<Result<bool>> Handle(UnblockUserRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Find the block
        var userBlock = await dbContext.UserBlocks
            .FirstOrDefaultAsync(ub => ub.BlockerId == userId && ub.BlockedId == request.UserId, cancellationToken);

        if (userBlock == null)
        {
            return Error.NotFound("BLOCK_NOT_FOUND", "User is not blocked");
        }

        // Hard delete the block
        dbContext.UserBlocks.Remove(userBlock);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Notify the unblocked user directly after save
        await notificationService.NotifyUserAsync(request.UserId, "Notify_UserUnblocked", new
        {
            blockerId = userId,
            blockedId = request.UserId
        }, cancellationToken);

        // Remove from Redis cache
        var db = redis.GetDatabase();
        var redisKey = $"{_redisOptions.ChannelPrefix}:blocks:{userId}";
        await db.SetRemoveAsync(redisKey, request.UserId).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} unblocked user {UnblockedUserId}",
            userId, request.UserId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/users/@me/blocks/{userId:long}", async (
            long userId,
            [FromServices] UnblockUserHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new UnblockUserRequest(userId), ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UnblockUser")
        .WithTags("Blocks");
}
