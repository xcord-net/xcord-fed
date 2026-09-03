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
                "Preset must be one of: Grid, Spotlight, Pip, SideBySide, AudioShow");
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
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The compositor is told the new preset over the room's control channel.
        // Restarting the egress would reapply it too, but only by dropping every
        // RTMP push and rebuilding it - a visible outage on each relay platform
        // for what is a change of arrangement, not of destination.
        try
        {
            await egressBuilder.PublishLayoutAsync(
                broadcast.ChannelId, newPreset, broadcast.StageSlots, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to publish layout change for broadcast {BroadcastId}",
                broadcast.Id);
            return Error.Failure(
                "LAYOUT_PUBLISH_FAILED",
                "Failed to apply the layout change to the live stream");
        }

        await notificationService.NotifyConversationAsync(
            broadcast.Channel.ConversationId,
            "Broadcast_LayoutChanged",
            new
            {
                broadcastId = broadcast.Id,
                channelId = broadcast.ChannelId,
                newPreset = newPreset.ToString()
            }, cancellationToken);

        logger.LogInformation(
            "User {UserId} changed broadcast {BroadcastId} layout to {Preset}",
            userId, broadcast.Id, newPreset);

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
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateBroadcastLayout")
        .WithTags("Broadcasts");
    }
}
