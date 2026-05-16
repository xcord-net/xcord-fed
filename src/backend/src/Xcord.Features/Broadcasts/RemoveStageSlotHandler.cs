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

public sealed record RemoveStageSlotCommand(long BroadcastId, long UserId);

public sealed class RemoveStageSlotHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILiveKitService livekitService,
    INotificationService notificationService,
    BroadcastEgressBuilder egressBuilder,
    ILogger<RemoveStageSlotHandler> logger)
    : IRequestHandler<RemoveStageSlotCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        RemoveStageSlotCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var currentUserId = userIdResult.Value;

        var broadcast = await dbContext.Broadcasts
            .Include(b => b.Channel)
            .Include(b => b.StageSlots)
            .FirstOrDefaultAsync(b => b.Id == request.BroadcastId, cancellationToken);

        if (broadcast == null)
            return Error.NotFound("BROADCAST_NOT_FOUND", "Broadcast not found");

        var permissionResult = await roleService.EnsureChannelRole(
            currentUserId, broadcast.ChannelId, Role.ManageBroadcasts);
        if (permissionResult.IsFailure)
            return permissionResult.Error;

        if (broadcast.Status != BroadcastStatus.Live
            && broadcast.Status != BroadcastStatus.Starting)
            return Error.Conflict(
                "BROADCAST_NOT_ACTIVE",
                "Stage can only be modified while the broadcast is Starting or Live");

        var slot = broadcast.StageSlots.FirstOrDefault(s => s.UserId == request.UserId);
        if (slot == null)
            return Error.NotFound("SLOT_NOT_FOUND", "User is not on the broadcast stage");

        dbContext.BroadcastStageSlots.Remove(slot);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var remainingSlots = broadcast.StageSlots
            .Where(s => s.Id != slot.Id)
            .OrderBy(s => s.SlotIndex)
            .ToList();

        var roomName = egressBuilder.BuildRoomName(broadcast.ChannelId);
        var templateUrl = egressBuilder.BuildTemplateUrl(
            broadcast.Id, broadcast.LayoutPreset, remainingSlots, roomName);
        var outputs = await egressBuilder.BuildOutputsAsync(broadcast.Id, cancellationToken).ConfigureAwait(false);

        string newEgressId;
        try
        {
            newEgressId = await livekitService.RestartEgressWithNewOutputsAsync(
                oldEgressId: broadcast.EgressJobId,
                roomName: roomName,
                templateUrl: templateUrl,
                outputs: outputs,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to restart egress for broadcast {BroadcastId} on stage remove",
                broadcast.Id);
            return Error.Failure(
                "EGRESS_RESTART_FAILED",
                "Failed to apply stage change to the egress pipeline");
        }

        broadcast.EgressJobId = newEgressId;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await notificationService.NotifyConversationAsync(
            broadcast.Channel.ConversationId,
            "Broadcast_StageChanged",
            new
            {
                broadcastId = broadcast.Id,
                channelId = broadcast.ChannelId,
                stageSlots = remainingSlots
                    .Select(s => new BroadcastStageSlotDto(s.UserId, s.SlotIndex))
                    .ToArray()
            }, cancellationToken);

        logger.LogInformation(
            "User {ActorId} removed user {UserId} from broadcast {BroadcastId}",
            currentUserId, request.UserId, broadcast.Id);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/broadcasts/{broadcastId:long}/stage/{userId:long}", async (
            long broadcastId,
            long userId,
            [FromServices] RemoveStageSlotHandler handler,
            CancellationToken ct) =>
        {
            var command = new RemoveStageSlotCommand(broadcastId, userId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("RemoveBroadcastStageSlot")
        .WithTags("Broadcasts");
    }
}
