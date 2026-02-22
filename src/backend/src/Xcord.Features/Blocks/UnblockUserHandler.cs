using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Claims;
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
    IHttpContextAccessor httpContextAccessor,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions,
    IOutboxWriter outboxWriter,
    ILogger<UnblockUserHandler> logger) : IRequestHandler<UnblockUserRequest, Result<bool>>
{
    private readonly RedisOptions _redisOptions = redisOptions.Value;

    public async Task<Result<bool>> Handle(UnblockUserRequest request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Find the block
        var userBlock = await dbContext.UserBlocks
            .FirstOrDefaultAsync(ub => ub.BlockerId == userId && ub.BlockedId == request.UserId, cancellationToken);

        if (userBlock == null)
        {
            return Error.NotFound("BLOCK_NOT_FOUND", "User is not blocked");
        }

        // Hard delete the block
        dbContext.UserBlocks.Remove(userBlock);

        // Write outbox event
        await outboxWriter.WriteAsync(
            dbContext,
            "User.Unblocked",
            new
            {
                blockerId = userId,
                blockedId = request.UserId
            },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Remove from Redis cache
        var db = redis.GetDatabase();
        var redisKey = $"{_redisOptions.ChannelPrefix}:blocks:{userId}";
        await db.SetRemoveAsync(redisKey, request.UserId);

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
            return await handler.ExecuteAsync(new UnblockUserRequest(userId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UnblockUser")
        .WithTags("Blocks");
}
