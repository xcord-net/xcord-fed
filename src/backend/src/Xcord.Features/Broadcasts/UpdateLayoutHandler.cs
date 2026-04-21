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

public sealed record UpdateLayoutCommand(long BroadcastId, string Preset);

public sealed record UpdateLayoutRequest(string Preset);

public sealed class UpdateLayoutHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILiveKitService livekitService,
    INotificationService notificationService,
    BroadcastEgressBuilder egressBuilder,
    ILogger<UpdateLayoutHandler> logger)
    : IRequestHandler<UpdateLayoutCommand, Result<bool>>,
      IValidatable<UpdateLayoutCommand>
{
    public Error? Validate(UpdateLayoutCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Preset))
            return Error.Validation("VALIDATION_ERROR", "Preset is required");
        if (!Enum.TryParse<BroadcastLayoutPreset>(request.Preset, ignoreCase: true, out _))
            return Error.Validation("VALIDATION_ERROR",
                "Preset must be one of: Grid, Spotlight, Pip, SideBySide");
        return null;
    }

    public async Task<Result<bool>> Handle(
        UpdateLayoutCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var broadcast = await dbContext.Broadcasts
            .Include(b => b.Channel)
            .Include(b => b.StageSlots)
            .FirstOrDefaultAsync(b => b.Id == request.BroadcastId, cancellationToken);

        if (broadcast == null)
            return Error.NotFound("BROADCAST_NOT_FOUND", "Broadcast not found");

        var permissionResult = await roleService.EnsureChannelRole(
            userId, broadcast.ChannelId, Role.ManageBroadcasts);
        if (permissionResult.IsFailure)
            return permissionResult.Error;

        if (broadcast.Status != BroadcastStatus.Live
            && broadcast.Status != BroadcastStatus.Starting)
            return Error.Conflict(
                "BROADCAST_NOT_ACTIVE",
                "Layout can only be changed while the broadcast is Starting or Live");

        var newPreset = Enum.Parse<BroadcastLayoutPreset>(request.Preset, ignoreCase: true);
        if (newPreset == broadcast.LayoutPreset)
            return true; // no-op

        broadcast.LayoutPreset = newPreset;
        await dbContext.SaveChangesAsync(cancellationToken);

        var roomName = egressBuilder.BuildRoomName(broadcast.ChannelId);

        // Rebuild the template URL for the new preset + existing stage slots, and
        // re-decrypt stream keys fresh so no plaintext copies linger between restarts.
        var templateUrl = egressBuilder.BuildTemplateUrl(
            broadcast.Id, newPreset, broadcast.StageSlots, roomName);
        var outputs = await egressBuilder.BuildOutputsAsync(broadcast.Id, cancellationToken);

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
                "Failed to restart egress for broadcast {BroadcastId} on layout change",
                broadcast.Id);
            return Error.Failure(
                "EGRESS_RESTART_FAILED",
                "Failed to apply layout change to the egress pipeline");
        }

        broadcast.EgressJobId = newEgressId;
        await dbContext.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyConversationAsync(
            broadcast.Channel.ConversationId,
            "Broadcast_LayoutChanged",
            new
            {
                broadcastId = broadcast.Id,
                channelId = broadcast.ChannelId,
                newPreset = newPreset.ToString()
            });

        logger.LogInformation(
            "User {UserId} changed broadcast {BroadcastId} layout to {Preset} (egress={EgressId})",
            userId, broadcast.Id, newPreset, newEgressId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/broadcasts/{broadcastId:long}/layout", async (
            long broadcastId,
            [FromBody] UpdateLayoutRequest request,
            [FromServices] UpdateLayoutHandler handler,
            CancellationToken ct) =>
        {
            var command = new UpdateLayoutCommand(broadcastId, request.Preset);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateBroadcastLayout")
        .WithTags("Broadcasts");
    }
}
