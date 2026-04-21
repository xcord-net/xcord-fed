using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Streambots;

public sealed record ListStreambotsCommand(long ChannelId);

public sealed record StreamBotDto(
    long Id,
    long ChannelId,
    string Name,
    StreamBotPlatform Platform,
    string RtmpUrl,
    bool IsDefault,
    bool HasStreamKey,
    DateTimeOffset CreatedAt
);

public sealed class ListStreambotsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService)
    : IRequestHandler<ListStreambotsCommand, Result<List<StreamBotDto>>>
{
    public async Task<Result<List<StreamBotDto>>> Handle(
        ListStreambotsCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify channel exists
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");

        // Require ManageBroadcasts at the channel level
        var permissionResult = await roleService.EnsureChannelRole(
            userId, channel.Id, Role.ManageBroadcasts);

        if (permissionResult.IsFailure)
            return permissionResult.Error;

        // Return all non-deleted streambots. EncryptedStreamKey is never leaked;
        // we only surface a boolean indicating whether one is configured.
        var streambots = await dbContext.StreamBots
            .AsNoTracking()
            .Where(s => s.ChannelId == request.ChannelId)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new StreamBotDto(
                s.Id,
                s.ChannelId,
                s.Name,
                s.Platform,
                s.RtmpUrl,
                s.IsDefault,
                s.EncryptedStreamKey.Length > 0,
                s.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return streambots;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/channels/{channelId:long}/streambots", async (
            long channelId,
            [FromServices] ListStreambotsHandler handler,
            CancellationToken ct) =>
        {
            var command = new ListStreambotsCommand(channelId);
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListStreambots")
        .WithTags("Streambots");
    }
}
