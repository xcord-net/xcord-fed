using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Broadcasts;

public sealed record JoinBroadcastAsGuestCommand(long BroadcastId);

public sealed record JoinBroadcastAsGuestResponse(
    string Token,
    string RoomName,
    string LivekitUrl);

public sealed class JoinBroadcastAsGuestHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILiveKitService livekitService,
    BroadcastEgressBuilder egressBuilder,
    IOptions<LiveKitOptions> livekitOptions,
    ILogger<JoinBroadcastAsGuestHandler> logger)
    : IRequestHandler<JoinBroadcastAsGuestCommand, Result<JoinBroadcastAsGuestResponse>>
{
    public async Task<Result<JoinBroadcastAsGuestResponse>> Handle(
        JoinBroadcastAsGuestCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var broadcast = await dbContext.Broadcasts
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BroadcastId, cancellationToken);
        if (broadcast == null)
            return Error.NotFound("BROADCAST_NOT_FOUND", "Broadcast not found");

        // Anyone with ViewBroadcast can request a guest token; the host decides who
        // actually gets placed on a stage slot (via AddStageSlot). Until they're on
        // stage, the token is effectively spectator-only at the compositor level.
        var permissionResult = await roleService.EnsureChannelRole(
            userId, broadcast.ChannelId, Role.ViewBroadcast);
        if (permissionResult.IsFailure)
            return permissionResult.Error;

        if (broadcast.Status != BroadcastStatus.Live
            && broadcast.Status != BroadcastStatus.Starting)
            return Error.Conflict(
                "BROADCAST_NOT_ACTIVE",
                "Cannot join a broadcast that is not Starting or Live");

        // Screen share gated on the same server permission as voice channels.
        var channelPerms = await roleService.GetChannelRoles(userId, broadcast.ChannelId);
        var canScreenShare = (channelPerms & (long)Role.ShareScreen) != 0;

        var roomName = egressBuilder.BuildRoomName(broadcast.ChannelId);

        var token = livekitService.GenerateToken(
            userId: userId,
            roomName: roomName,
            canPublish: true,
            canSubscribe: true,
            canPublishData: true,
            canScreenShare: canScreenShare,
            ttl: TimeSpan.FromHours(2));

        logger.LogInformation(
            "Issued guest broadcast token to user {UserId} for broadcast {BroadcastId}",
            userId, broadcast.Id);

        return new JoinBroadcastAsGuestResponse(
            Token: token,
            RoomName: roomName,
            LivekitUrl: livekitOptions.Value.Host);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/broadcasts/{broadcastId:long}/guest-token", async (
            long broadcastId,
            [FromServices] JoinBroadcastAsGuestHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(
                new JoinBroadcastAsGuestCommand(broadcastId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("JoinBroadcastAsGuest")
        .WithTags("Broadcasts");
    }
}
