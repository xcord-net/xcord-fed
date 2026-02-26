using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Notifications;

public sealed record DeleteNotificationSettingRequest(
    long SettingId
);

public sealed class DeleteNotificationSettingHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions) : IRequestHandler<DeleteNotificationSettingRequest, Result<bool>>
{
    private readonly string _prefix = redisOptions.Value.ChannelPrefix;

    public async Task<Result<bool>> Handle(DeleteNotificationSettingRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Find the setting
        var setting = await dbContext.NotificationSettings
            .FirstOrDefaultAsync(ns => ns.Id == request.SettingId, cancellationToken);

        if (setting == null)
        {
            return Error.NotFound("SETTING_NOT_FOUND", "Notification setting not found");
        }

        // Ensure the setting belongs to the current user
        if (setting.UserId != userId)
        {
            return Error.Forbidden("NOT_YOUR_SETTING", "You can only delete your own notification settings");
        }

        // Delete the setting
        dbContext.NotificationSettings.Remove(setting);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await InvalidateCacheAsync(userId, setting.ServerId, setting.ChannelId);

        return true;
    }

    private async Task InvalidateCacheAsync(long userId, long? serverId, long? channelId)
    {
        var db = redis.GetDatabase();
        var cacheKey = $"{_prefix}:notif:{userId}:{serverId ?? 0}:{channelId ?? 0}";
        await db.KeyDeleteAsync(cacheKey);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/users/@me/notification-settings/{settingId:long}", async (
            long settingId,
            [FromServices] DeleteNotificationSettingHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new DeleteNotificationSettingRequest(settingId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteNotificationSetting")
        .WithTags("Notifications");
}
