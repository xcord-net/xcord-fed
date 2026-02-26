using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Presence;

public sealed record ActivityPayload(
    string Type,
    string Name,
    string? Details,
    string? Url
);

public sealed record UpdatePresenceCommand(
    string Status,
    ActivityPayload? Activity
);

public sealed class UpdatePresenceHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IPresenceService presenceService,
    IPresenceNotifier presenceNotifier,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions)
    : IRequestHandler<UpdatePresenceCommand, Result<bool>>, IValidatable<UpdatePresenceCommand>
{
    private const int RateLimitMax = 5;
    private const int RateLimitWindowSeconds = 60;

    private readonly string _prefix = redisOptions.Value.ChannelPrefix;

    public Error? Validate(UpdatePresenceCommand request)
    {
        if (!Enum.TryParse<PresenceStatus>(request.Status, ignoreCase: true, out _))
        {
            return Error.Validation("INVALID_STATUS",
                "Invalid status value. Valid values: Online, Away, DND, Offline");
        }

        if (request.Activity is not null)
        {
            if (!Enum.TryParse<ActivityType>(request.Activity.Type, ignoreCase: true, out _))
            {
                return Error.Validation("INVALID_ACTIVITY_TYPE",
                    "Invalid activity type. Valid values: Playing, Streaming, Listening, Watching, Custom, Competing");
            }

            if (string.IsNullOrWhiteSpace(request.Activity.Name))
            {
                return Error.Validation("VALIDATION_ERROR", "Activity name is required");
            }

            if (request.Activity.Name.Length > 128)
            {
                return Error.Validation("VALIDATION_ERROR", "Activity name must be 128 characters or less");
            }
        }

        return null;
    }

    public async Task<Result<bool>> Handle(UpdatePresenceCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Redis-based rate limiting: max 5 calls per minute per user
        var db = redis.GetDatabase();
        var rateLimitKey = $"{_prefix}presence_ratelimit:{userId}";
        var count = await db.StringIncrementAsync(rateLimitKey);
        if (count == 1)
        {
            // First call in this window — set the expiry
            await db.KeyExpireAsync(rateLimitKey, TimeSpan.FromSeconds(RateLimitWindowSeconds));
        }

        if (count > RateLimitMax)
        {
            return Error.RateLimited("RATE_LIMITED", "Presence update rate limit exceeded. Maximum 5 updates per minute.");
        }

        // Parse and set status
        Enum.TryParse<PresenceStatus>(request.Status, ignoreCase: true, out var presenceStatus);
        await presenceService.SetStatusAsync(userId, presenceStatus);

        // Get all servers the user is a member of
        var serverIds = await dbContext.ServerMembers
            .Where(sm => sm.UserId == userId)
            .Select(sm => sm.ServerId)
            .ToListAsync(cancellationToken);

        // Update heartbeat for non-offline statuses
        if (presenceStatus != PresenceStatus.Offline)
        {
            await presenceService.HeartbeatAsync(userId, serverIds);
        }

        // Fan out Presence_Updated event to all servers the user belongs to
        await presenceNotifier.NotifyPresenceChangedAsync(userId, presenceStatus, serverIds);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/users/@me/presence", async (
            UpdatePresenceCommand request,
            [Microsoft.AspNetCore.Mvc.FromServices] UpdatePresenceHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(request, ct, _ => Results.NoContent()))
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdatePresence")
        .WithTags("Presence");
}
