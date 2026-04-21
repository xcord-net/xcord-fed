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

public sealed record UpdateActiveStreambotsCommand(
    long BroadcastId,
    IReadOnlyList<long> StreambotIds);

public sealed record UpdateActiveStreambotsRequest(long[] StreambotIds);

public sealed class UpdateActiveStreambotsHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILiveKitService livekitService,
    INotificationService notificationService,
    BroadcastEgressBuilder egressBuilder,
    ILogger<UpdateActiveStreambotsHandler> logger)
    : IRequestHandler<UpdateActiveStreambotsCommand, Result<bool>>,
      IValidatable<UpdateActiveStreambotsCommand>
{
    public Error? Validate(UpdateActiveStreambotsCommand request)
    {
        if (request.StreambotIds == null)
            return Error.Validation("VALIDATION_ERROR",
                "StreambotIds is required (use [] to disable all relays)");
        return null;
    }

    public async Task<Result<bool>> Handle(
        UpdateActiveStreambotsCommand request, CancellationToken cancellationToken)
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
                "Streambots can only be changed while the broadcast is Starting or Live");

        var desiredIds = request.StreambotIds.Distinct().ToHashSet();

        // Validate all requested bots belong to this channel (same defense as StartBroadcast).
        if (desiredIds.Count > 0)
        {
            var validBotIds = await dbContext.StreamBots
                .Where(s => s.ChannelId == broadcast.ChannelId && desiredIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);
            var invalid = desiredIds.Except(validBotIds).ToArray();
            if (invalid.Length > 0)
                return Error.Validation(
                    "STREAMBOT_INVALID",
                    $"One or more stream bots do not belong to this channel: {string.Join(", ", invalid)}");
        }

        var existing = await dbContext.BroadcastStreambots
            .Where(bs => bs.BroadcastId == broadcast.Id)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        // Anything currently active-or-connecting that isn't in the desired set → end it.
        foreach (var bs in existing)
        {
            var stillActive = bs.Status != BroadcastStreambotStatus.Ended
                && bs.Status != BroadcastStreambotStatus.Failed;
            if (stillActive && !desiredIds.Contains(bs.StreamBotId))
            {
                bs.Status = BroadcastStreambotStatus.Ended;
                bs.EndedAt = now;
            }
        }

        // Anything in the desired set without an active row → revive an existing row if
        // present (unique (BroadcastId, StreamBotId) index forbids a second row), else add.
        var existingByBotId = existing.ToDictionary(bs => bs.StreamBotId);
        foreach (var botId in desiredIds)
        {
            if (existingByBotId.TryGetValue(botId, out var bs))
            {
                var stillActive = bs.Status != BroadcastStreambotStatus.Ended
                    && bs.Status != BroadcastStreambotStatus.Failed;
                if (stillActive) continue;
                bs.Status = BroadcastStreambotStatus.Connecting;
                bs.StartedAt = now;
                bs.EndedAt = null;
                bs.LastError = null;
            }
            else
            {
                dbContext.BroadcastStreambots.Add(new BroadcastStreambot
                {
                    Id = snowflakeGenerator.NextId(),
                    BroadcastId = broadcast.Id,
                    StreamBotId = botId,
                    Status = BroadcastStreambotStatus.Connecting,
                    StartedAt = now
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var roomName = egressBuilder.BuildRoomName(broadcast.ChannelId);
        var templateUrl = egressBuilder.BuildTemplateUrl(
            broadcast.Id, broadcast.LayoutPreset, broadcast.StageSlots, roomName);
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
                "Failed to restart egress for broadcast {BroadcastId} on streambot change",
                broadcast.Id);
            return Error.Failure(
                "EGRESS_RESTART_FAILED",
                "Failed to apply streambot change to the egress pipeline");
        }

        broadcast.EgressJobId = newEgressId;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Snapshot the current streambots for the event payload. Clients use this to
        // replace their local state without a follow-up GET.
        var payload = await dbContext.BroadcastStreambots
            .AsNoTracking()
            .Where(bs => bs.BroadcastId == broadcast.Id)
            .Include(bs => bs.StreamBot)
            .Select(bs => new BroadcastStreambotDto(
                bs.StreamBotId,
                bs.StreamBot.Name,
                bs.Status.ToString(),
                bs.LastError))
            .ToListAsync(cancellationToken);

        await notificationService.NotifyConversationAsync(
            broadcast.Channel.ConversationId,
            "Broadcast_StreambotsChanged",
            new
            {
                broadcastId = broadcast.Id,
                channelId = broadcast.ChannelId,
                streambots = payload
            });

        logger.LogInformation(
            "User {UserId} updated active streambots for broadcast {BroadcastId} ({Count} active)",
            userId, broadcast.Id, desiredIds.Count);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPut("/api/v1/broadcasts/{broadcastId:long}/streambots", async (
            long broadcastId,
            [FromBody] UpdateActiveStreambotsRequest request,
            [FromServices] UpdateActiveStreambotsHandler handler,
            CancellationToken ct) =>
        {
            var command = new UpdateActiveStreambotsCommand(
                broadcastId, request.StreambotIds ?? Array.Empty<long>());
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateBroadcastStreambots")
        .WithTags("Broadcasts");
    }
}
