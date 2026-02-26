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
    DateTimeOffset? MuteUntil
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
    IOptions<RedisOptions> redisOptions) : IRequestHandler<UpdateNotificationSettingRequest, Result<UpdateNotificationSettingResponse>>
{
    private readonly string _prefix = redisOptions.Value.ChannelPrefix;

    public async Task<Result<UpdateNotificationSettingResponse>> Handle(UpdateNotificationSettingRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Validate that server/channel exist if provided
        if (request.ServerId.HasValue)
        {
            var serverExists = await dbContext.Servers.AnyAsync(s => s.Id == request.ServerId.Value, cancellationToken);
            if (!serverExists)
            {
                return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
            }

            // Check if user is a member
            var isMember = await dbContext.ServerMembers
                .AnyAsync(sm => sm.UserId == userId && sm.ServerId == request.ServerId.Value, cancellationToken);
            if (!isMember)
            {
                return Error.Forbidden("NOT_A_MEMBER", "You must be a member of this server");
            }
        }

        if (request.ChannelId.HasValue)
        {
            var channelExists = await dbContext.Channels.AnyAsync(c => c.Id == request.ChannelId.Value, cancellationToken);
            if (!channelExists)
            {
                return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
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

        await dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await InvalidateCacheAsync(userId, request.ServerId, request.ChannelId);

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
        await db.KeyDeleteAsync(cacheKey);
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
