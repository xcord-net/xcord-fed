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

public sealed record AddStageSlotCommand(long BroadcastId, long UserId, int SlotIndex);

public sealed record AddStageSlotRequest(long UserId, int SlotIndex);

public sealed class AddStageSlotHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILiveKitService livekitService,
    INotificationService notificationService,
    BroadcastEgressBuilder egressBuilder,
    IOptions<TierOptions> tierOptions,
    ILogger<AddStageSlotHandler> logger)
    : IRequestHandler<AddStageSlotCommand, Result<bool>>,
      IValidatable<AddStageSlotCommand>
{
    public Error? Validate(AddStageSlotCommand request)
    {
        if (request.SlotIndex < 0)
            return Error.Validation("VALIDATION_ERROR", "SlotIndex must be non-negative");
        return null;
    }

    public async Task<Result<bool>> Handle(
        AddStageSlotCommand request, CancellationToken cancellationToken)
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

        var maxStageSize = tierOptions.Value.MaxBroadcastStageSize;
        if (maxStageSize <= 0) maxStageSize = 8;
        if (request.SlotIndex >= maxStageSize)
            return Error.Validation(
                "SLOT_INDEX_OUT_OF_RANGE",
                $"SlotIndex must be less than the tier stage cap of {maxStageSize}");

        // Pre-check uniqueness - the DB index catches this too, but surfacing a named
        // error avoids leaking a raw EF unique-violation to the client.
        if (broadcast.StageSlots.Any(s => s.SlotIndex == request.SlotIndex))
            return Error.Conflict(
                "SLOT_OCCUPIED",
                $"Slot index {request.SlotIndex} is already occupied");
        if (broadcast.StageSlots.Any(s => s.UserId == request.UserId))
            return Error.Conflict(
                "USER_ALREADY_ON_STAGE",
                "User is already on the broadcast stage");

        // The user being added must be a member of the channel's server - otherwise
        // they couldn't actually connect to the LiveKit room and the slot would be
        // permanently empty in the composition.
        var isMember = await dbContext.ServerMembers
            .AnyAsync(sm => sm.UserId == request.UserId
                && sm.ServerId == broadcast.Channel.ServerId,
                cancellationToken);
        if (!isMember)
            return Error.Validation(
                "USER_NOT_IN_SERVER",
                "User is not a member of the channel's server");

        var now = DateTimeOffset.UtcNow;
        var slot = new BroadcastStageSlot
        {
            Id = snowflakeGenerator.NextId(),
            BroadcastId = broadcast.Id,
            UserId = request.UserId,
            SlotIndex = request.SlotIndex,
            AddedAt = now
        };
        dbContext.BroadcastStageSlots.Add(slot);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Refresh stage slots from the tracker (they now include the new slot).
        var updatedSlots = broadcast.StageSlots
            .Append(slot)
            .OrderBy(s => s.SlotIndex)
            .ToList();

        var roomName = egressBuilder.BuildRoomName(broadcast.ChannelId);
        var templateUrl = egressBuilder.BuildTemplateUrl(
            broadcast.Id, broadcast.LayoutPreset, updatedSlots, roomName);
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
                "Failed to restart egress for broadcast {BroadcastId} on stage add",
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
                stageSlots = updatedSlots
                    .Select(s => new BroadcastStageSlotDto(s.UserId, s.SlotIndex))
                    .ToArray()
            }, cancellationToken);

        logger.LogInformation(
            "User {ActorId} added user {UserId} to slot {SlotIndex} on broadcast {BroadcastId}",
            currentUserId, request.UserId, request.SlotIndex, broadcast.Id);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/broadcasts/{broadcastId:long}/stage", async (
            long broadcastId,
            [FromBody] AddStageSlotRequest request,
            [FromServices] AddStageSlotHandler handler,
            CancellationToken ct) =>
        {
            var command = new AddStageSlotCommand(broadcastId, request.UserId, request.SlotIndex);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("AddBroadcastStageSlot")
        .WithTags("Broadcasts");
    }
}
