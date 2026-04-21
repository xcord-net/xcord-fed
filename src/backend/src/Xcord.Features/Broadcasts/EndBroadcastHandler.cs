using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Broadcasts;

public sealed record EndBroadcastCommand(long BroadcastId);

public sealed class EndBroadcastHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILiveKitService livekitService,
    INotificationService notificationService,
    ILogger<EndBroadcastHandler> logger)
    : IRequestHandler<EndBroadcastCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        EndBroadcastCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var broadcast = await dbContext.Broadcasts
            .Include(b => b.Channel)
            .FirstOrDefaultAsync(b => b.Id == request.BroadcastId, cancellationToken);

        if (broadcast == null)
            return Error.NotFound("BROADCAST_NOT_FOUND", "Broadcast not found");

        // The host can always end their own broadcast. Other users need ManageBroadcasts
        // on the channel (e.g. moderators stopping a misbehaving stream).
        if (broadcast.HostUserId != userId)
        {
            var permissionResult = await roleService.EnsureChannelRole(
                userId, broadcast.ChannelId, Role.ManageBroadcasts);
            if (permissionResult.IsFailure)
                return permissionResult.Error;
        }

        // Idempotent: already-ended broadcasts succeed without side effects.
        if (broadcast.Status == BroadcastStatus.Ended)
            return true;

        // Best-effort egress stop. A failed egress pipeline may already be gone, and
        // the webhook handler is the authoritative source for egress lifecycle - we
        // don't want to block ending the broadcast on LiveKit connectivity.
        if (!string.IsNullOrEmpty(broadcast.EgressJobId))
        {
            try
            {
                await livekitService.StopEgressAsync(broadcast.EgressJobId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "StopEgress failed for broadcast {BroadcastId} (egress={EgressId}); ending anyway",
                    broadcast.Id, broadcast.EgressJobId);
            }
        }

        var now = DateTimeOffset.UtcNow;
        broadcast.Status = BroadcastStatus.Ended;
        broadcast.EndedAt = now;

        var streambots = await dbContext.BroadcastStreambots
            .Where(bs => bs.BroadcastId == broadcast.Id)
            .ToListAsync(cancellationToken);
        foreach (var bs in streambots)
        {
            if (bs.Status == BroadcastStreambotStatus.Ended
                || bs.Status == BroadcastStreambotStatus.Failed)
                continue;
            bs.Status = BroadcastStreambotStatus.Ended;
            bs.EndedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyConversationAsync(
            broadcast.Channel.ConversationId,
            "Broadcast_Ended",
            new
            {
                broadcastId = broadcast.Id,
                channelId = broadcast.ChannelId,
                endedAt = now
            });

        logger.LogInformation(
            "User {UserId} ended broadcast {BroadcastId}",
            userId, broadcast.Id);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/broadcasts/{broadcastId:long}", async (
            long broadcastId,
            [FromServices] EndBroadcastHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(
                new EndBroadcastCommand(broadcastId), ct,
                _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("EndBroadcast")
        .WithTags("Broadcasts");
    }
}
