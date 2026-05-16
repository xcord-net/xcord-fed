using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Notifications;

public sealed record UpdateNotificationSettingRequest(
    long? ServerId,
    long? ChannelId,
    NotificationLevel Level,
    bool? SuppressEveryone,
    bool? SuppressRoles,
    DateTimeOffset? MuteUntil,
    bool? MuteAll = null
);

public sealed record UpdateNotificationSettingResponse(
    long Id,
    long UserId,
    long? ServerId,
    long? ChannelId,
    NotificationLevel Level,
    bool SuppressEveryone,
    bool SuppressRoles,
    DateTimeOffset? MuteUntil
);

public sealed class UpdateNotificationSettingHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    SnowflakeIdGenerator snowflakeIdGenerator,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions)
    : IRequestHandler<UpdateNotificationSettingRequest, Result<UpdateNotificationSettingResponse>>,
      IValidatable<UpdateNotificationSettingRequest>
{
    private readonly string _prefix = redisOptions.Value.ChannelPrefix;

    public Error? Validate(UpdateNotificationSettingRequest request)
    {
        // Optional IDs, when provided, must be positive Snowflake IDs. A zero
        // or negative value can only come from a malformed client and would
        // otherwise reach the DB layer as a silent miss.
        if (request.ServerId.HasValue && request.ServerId.Value <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be greater than zero");
        }

        if (request.ChannelId.HasValue && request.ChannelId.Value <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ChannelId must be greater than zero");
        }

        if (!Enum.IsDefined(typeof(NotificationLevel), request.Level))
        {
            return Error.Validation("VALIDATION_ERROR", "Level must be a defined NotificationLevel value");
        }

        // A MuteUntil in the distant past is meaningless. Reject anything
        // earlier than 1 minute ago to allow tiny clock skew but block bad input.
        if (request.MuteUntil.HasValue &&
            request.MuteUntil.Value < DateTimeOffset.UtcNow.AddMinutes(-1))
        {
            return Error.Validation("VALIDATION_ERROR", "MuteUntil must not be in the past");
        }

        return null;
    }

    public async Task<Result<UpdateNotificationSettingResponse>> Handle(UpdateNotificationSettingRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Validate that server/channel exist if provided. These reads do not mutate state,
        // so they happen outside the transaction.
        if (request.ServerId.HasValue)
        {
            var serverExists = await dbContext.Servers.AnyAsync(s => s.Id == request.ServerId.Value, cancellationToken).ConfigureAwait(false);
            if (!serverExists)
            {
                return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
            }

            // Check if user is a member
            var memberCheck = await dbContext.EnsureMembership(request.ServerId.Value, userId, cancellationToken).ConfigureAwait(false);
            if (memberCheck.IsFailure) return memberCheck.Error;
        }

        if (request.ChannelId.HasValue)
        {
            var channelExists = await dbContext.Channels.AnyAsync(c => c.Id == request.ChannelId.Value, cancellationToken).ConfigureAwait(false);
            if (!channelExists)
            {
                return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
            }
        }

        // Wrap both writes (User.MuteAll toggle + NotificationSetting upsert) in a single
        // transaction so a failure in either rolls back the other. Without this, a successful
        // MuteAll flip could persist while the per-channel setting fails to save, leaving the
        // user in an inconsistent state.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Handle global MuteAll toggle (stored on User entity)
        if (request.MuteAll.HasValue)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
            if (user != null)
            {
                user.MuteAll = request.MuteAll.Value;
            }
        }

        // Find existing setting or create new one
        var setting = await dbContext.NotificationSettings
            .FirstOrDefaultAsync(ns => ns.UserId == userId &&
                                     ns.ServerId == request.ServerId &&
                                     ns.ChannelId == request.ChannelId,
                                cancellationToken);

        if (setting == null)
        {
            // Create new setting
            setting = new NotificationSetting
            {
                Id = snowflakeIdGenerator.NextId(),
                UserId = userId,
                ServerId = request.ServerId,
                ChannelId = request.ChannelId,
                Level = request.Level,
                SuppressEveryone = request.SuppressEveryone ?? false,
                SuppressRoles = request.SuppressRoles ?? false,
                MuteUntil = request.MuteUntil
            };
            dbContext.NotificationSettings.Add(setting);
        }
        else
        {
            // Update existing setting
            setting.Level = request.Level;
            if (request.SuppressEveryone.HasValue)
            {
                setting.SuppressEveryone = request.SuppressEveryone.Value;
            }
            if (request.SuppressRoles.HasValue)
            {
                setting.SuppressRoles = request.SuppressRoles.Value;
            }
            setting.MuteUntil = request.MuteUntil;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // Invalidate cache after a successful commit so readers do not re-populate it from
        // pre-commit state.
        await InvalidateCacheAsync(userId, request.ServerId, request.ChannelId).ConfigureAwait(false);

        return new UpdateNotificationSettingResponse(
            Id: setting.Id,
            UserId: setting.UserId,
            ServerId: setting.ServerId,
            ChannelId: setting.ChannelId,
            Level: setting.Level,
            SuppressEveryone: setting.SuppressEveryone,
            SuppressRoles: setting.SuppressRoles,
            MuteUntil: setting.MuteUntil
        );
    }

    private async Task InvalidateCacheAsync(long userId, long? serverId, long? channelId)
    {
        var db = redis.GetDatabase();
        var cacheKey = $"{_prefix}:notif:{userId}:{serverId ?? 0}:{channelId ?? 0}";
        await db.KeyDeleteAsync(cacheKey).ConfigureAwait(false);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/users/@me/notification-settings", async (
            [FromBody] UpdateNotificationSettingRequest request,
            [FromServices] UpdateNotificationSettingHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(request, ct))
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("UpdateNotificationSetting")
            .WithTags("Notifications");
}
