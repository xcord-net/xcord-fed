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

public sealed record StartBroadcastCommand(
    long ChannelId,
    string LayoutPreset,
    IReadOnlyList<long> StreambotIds);

public sealed record StartBroadcastRequest(
    string LayoutPreset,
    long[] StreambotIds);

public sealed record StartBroadcastResponse(
    long BroadcastId,
    string PublishToken,
    string RoomName,
    string LivekitUrl,
    string HlsUrl);

public sealed class StartBroadcastHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILiveKitService livekitService,
    INotificationService notificationService,
    BroadcastEgressBuilder egressBuilder,
    IOptions<LiveKitOptions> livekitOptions,
    ILogger<StartBroadcastHandler> logger)
    : IRequestHandler<StartBroadcastCommand, Result<StartBroadcastResponse>>,
      IValidatable<StartBroadcastCommand>
{
    public Error? Validate(StartBroadcastCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.LayoutPreset))
            return Error.Validation("VALIDATION_ERROR", "LayoutPreset is required");
        if (!Enum.TryParse<BroadcastLayoutPreset>(request.LayoutPreset, ignoreCase: true, out _))
            return Error.Validation("VALIDATION_ERROR",
                "LayoutPreset must be one of: Grid, Spotlight, Pip, SideBySide");
        if (request.StreambotIds == null)
            return Error.Validation("VALIDATION_ERROR", "StreambotIds is required (use [] for none)");
        return null;
    }

    public async Task<Result<StartBroadcastResponse>> Handle(
        StartBroadcastCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");

        if ((channel.Capabilities & ChannelCapability.Streaming) == 0)
            return Error.Forbidden(
                "CHANNEL_NOT_STREAMING",
                "Channel does not have the Streaming capability enabled");

        var permissionResult = await roleService.EnsureChannelRole(
            userId, channel.Id, Role.ManageBroadcasts);
        if (permissionResult.IsFailure)
            return permissionResult.Error;

        // A channel can only host one live/starting broadcast at a time.
        var existingActive = await dbContext.Broadcasts
            .AnyAsync(b => b.ChannelId == request.ChannelId
                && (b.Status == BroadcastStatus.Starting || b.Status == BroadcastStatus.Live),
                cancellationToken);
        if (existingActive)
            return Error.Conflict(
                "BROADCAST_ALREADY_ACTIVE",
                "Channel already has an active broadcast");

        var preset = Enum.Parse<BroadcastLayoutPreset>(request.LayoutPreset, ignoreCase: true);

        // Validate streambot ids belong to this channel - prevents activating a bot from
        // another channel's configuration, which would leak stream keys or route the feed
        // to an unauthorized destination.
        var distinctBotIds = request.StreambotIds.Distinct().ToArray();
        if (distinctBotIds.Length > 0)
        {
            var validBotIds = await dbContext.StreamBots
                .Where(s => s.ChannelId == request.ChannelId && distinctBotIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            var invalid = distinctBotIds.Except(validBotIds).ToArray();
            if (invalid.Length > 0)
                return Error.Validation(
                    "STREAMBOT_INVALID",
                    $"One or more stream bots do not belong to this channel: {string.Join(", ", invalid)}");
        }

        var broadcastId = snowflakeGenerator.NextId();
        var roomName = egressBuilder.BuildRoomName(channel.Id);
        var now = DateTimeOffset.UtcNow;

        // Persist the broadcast + its streambot relays first so the egress builder can
        // enumerate active bots from the DB. Status=Starting until the egress webhook
        // confirms the compositor is actually emitting segments.
        var broadcast = new Broadcast
        {
            Id = broadcastId,
            ChannelId = channel.Id,
            HostUserId = userId,
            EgressJobId = string.Empty, // filled after StartEgress succeeds
            LayoutPreset = preset,
            HlsPlaylistKey = $"{broadcastId}/playlist.m3u8",
            Status = BroadcastStatus.Starting,
            StartedAt = now
        };
        dbContext.Broadcasts.Add(broadcast);

        foreach (var botId in distinctBotIds)
        {
            dbContext.BroadcastStreambots.Add(new BroadcastStreambot
            {
                Id = snowflakeGenerator.NextId(),
                BroadcastId = broadcastId,
                StreamBotId = botId,
                Status = BroadcastStreambotStatus.Connecting,
                StartedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Build outputs & template URL, then fire the egress. If it fails we leave the
        // broadcast row in Starting status - the caller can retry or end it.
        var outputs = await egressBuilder.BuildOutputsAsync(broadcastId, cancellationToken).ConfigureAwait(false);
        var templateUrl = egressBuilder.BuildTemplateUrl(
            broadcastId, preset,
            slots: Array.Empty<BroadcastStageSlot>(),
            roomName: roomName);

        string egressJobId;
        try
        {
            egressJobId = await livekitService.StartRoomCompositeEgressAsync(
                roomName, templateUrl, outputs, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to start LiveKit egress for broadcast {BroadcastId}; marking as Failed",
                broadcastId);

            broadcast.Status = BroadcastStatus.Failed;
            broadcast.EndedAt = DateTimeOffset.UtcNow;
            foreach (var bs in dbContext.ChangeTracker.Entries<BroadcastStreambot>()
                         .Select(e => e.Entity)
                         .Where(e => e.BroadcastId == broadcastId))
            {
                bs.Status = BroadcastStreambotStatus.Failed;
                bs.EndedAt = DateTimeOffset.UtcNow;
                bs.LastError = "Failed to start egress";
            }
            await dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

            return Error.Failure(
                "EGRESS_START_FAILED",
                "Failed to start the broadcast egress pipeline");
        }

        broadcast.EgressJobId = egressJobId;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish token for the host - 30m TTL matching voice channel tokens.
        // Limitation: there is no broadcast token refresh endpoint yet, so broadcasts
        // longer than 30 minutes will require the host to restart the broadcast. A
        // RefreshBroadcastToken hub method should be added to mirror RefreshVoiceToken.
        var publishToken = livekitService.GenerateToken(
            userId: userId,
            roomName: roomName,
            canPublish: true,
            canSubscribe: true,
            canPublishData: true,
            canScreenShare: true,
            ttl: TimeSpan.FromMinutes(30));

        var hlsUrl = egressBuilder.BuildHlsUrl(broadcastId);

        // Fan out the start event to the channel conversation group so every active
        // client in the channel learns about the broadcast without polling.
        await notificationService.NotifyConversationAsync(
            channel.ConversationId,
            "Broadcast_Started",
            new
            {
                broadcastId,
                channelId = channel.Id,
                hostUserId = userId,
                layoutPreset = preset.ToString(),
                status = BroadcastStatus.Starting.ToString(),
                hlsUrl,
                roomName,
                startedAt = now
            }, cancellationToken);

        logger.LogInformation(
            "User {UserId} started broadcast {BroadcastId} on channel {ChannelId} (egress={EgressId})",
            userId, broadcastId, channel.Id, egressJobId);

        return new StartBroadcastResponse(
            BroadcastId: broadcastId,
            PublishToken: publishToken,
            RoomName: roomName,
            LivekitUrl: livekitOptions.Value.Host,
            HlsUrl: hlsUrl);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/channels/{channelId:long}/broadcasts", async (
            long channelId,
            [FromBody] StartBroadcastRequest request,
            [FromServices] StartBroadcastHandler handler,
            CancellationToken ct) =>
        {
            var command = new StartBroadcastCommand(
                ChannelId: channelId,
                LayoutPreset: request.LayoutPreset,
                StreambotIds: request.StreambotIds ?? Array.Empty<long>());

            return await handler.ExecuteAsync(
                command, ct,
                success => Results.Created($"/api/v1/broadcasts/{success.BroadcastId}", success));
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("StartBroadcast")
        .WithTags("Broadcasts");
    }
}
