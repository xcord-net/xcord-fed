using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Broadcasts;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Api;

public partial class MainHub
{
    /// <summary>
    /// Issues a fresh publish token for a broadcast the caller is hosting.
    /// </summary>
    /// <remarks>
    /// Broadcast publish tokens are short-lived, like voice tokens. Without a way
    /// to renew one, a broadcast simply stopped after thirty minutes and the host
    /// had to start it again - which also restarted every RTMP relay. Mirrors
    /// <see cref="RefreshVoiceToken"/>, including its behaviour on a revoked
    /// permission: the token is refused rather than quietly reissued.
    /// </remarks>
    public async Task<object> RefreshBroadcastToken(long broadcastId)
    {
        if (broadcastId <= 0) throw new HubException("Invalid id");

        var userId = GetUserId() ?? throw new HubException("Unauthorized");

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
        var liveKitService = scope.ServiceProvider.GetRequiredService<ILiveKitService>();
        var egressBuilder = scope.ServiceProvider.GetRequiredService<BroadcastEgressBuilder>();

        var broadcast = await context.Broadcasts
            .FirstOrDefaultAsync(b => b.Id == broadcastId && b.DeletedAt == null)
            ?? throw new HubException("Broadcast not found");

        if (broadcast.Status is not (BroadcastStatus.Starting or BroadcastStatus.Live))
            throw new HubException("Broadcast is not live");

        // Only someone who could have started it can keep publishing to it. The
        // host is checked first because they are the common case and do not need
        // a role lookup.
        if (broadcast.HostUserId != userId)
        {
            var permission = await roleService.EnsureChannelRole(
                userId, broadcast.ChannelId, Role.ManageBroadcasts);
            if (permission.IsFailure)
                throw new HubException("Forbidden");
        }

        var roomName = egressBuilder.BuildRoomName(broadcast.ChannelId);

        var token = liveKitService.GenerateToken(
            userId: userId,
            roomName: roomName,
            canPublish: true,
            canSubscribe: true,
            canPublishData: true,
            canScreenShare: true,
            ttl: TimeSpan.FromMinutes(30));

        _logger.LogInformation(
            "User {UserId} refreshed the publish token for broadcast {BroadcastId}",
            userId, broadcastId);

        return new { token, roomName, broadcastId };
    }
}
