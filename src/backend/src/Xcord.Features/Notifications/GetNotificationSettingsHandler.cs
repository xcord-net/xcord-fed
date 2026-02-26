using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Notifications;

public sealed record GetNotificationSettingsRequest(
    long? ServerId,
    long? ChannelId
);

public sealed record NotificationSettingDto(
    long Id,
    long UserId,
    long? ServerId,
    long? ChannelId,
    NotificationLevel Level,
    bool SuppressEveryone,
    bool SuppressRoles,
    DateTimeOffset? MuteUntil
);

public sealed class GetNotificationSettingsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<GetNotificationSettingsRequest, Result<List<NotificationSettingDto>>>
{
    public async Task<Result<List<NotificationSettingDto>>> Handle(GetNotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Build query based on filters
        var query = dbContext.NotificationSettings
            .Where(ns => ns.UserId == userId);

        if (request.ServerId.HasValue)
        {
            query = query.Where(ns => ns.ServerId == request.ServerId.Value);
        }

        if (request.ChannelId.HasValue)
        {
            query = query.Where(ns => ns.ChannelId == request.ChannelId.Value);
        }

        var settings = await query.ToListAsync(cancellationToken);

        var dtos = settings.Select(ns => new NotificationSettingDto(
            Id: ns.Id,
            UserId: ns.UserId,
            ServerId: ns.ServerId,
            ChannelId: ns.ChannelId,
            Level: ns.Level,
            SuppressEveryone: ns.SuppressEveryone,
            SuppressRoles: ns.SuppressRoles,
            MuteUntil: ns.MuteUntil
        )).ToList();

        return dtos;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/@me/notification-settings", async (
            [FromServices] GetNotificationSettingsHandler handler,
            long? serverId,
            long? channelId,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new GetNotificationSettingsRequest(serverId, channelId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetNotificationSettings")
        .WithTags("Notifications");
}
