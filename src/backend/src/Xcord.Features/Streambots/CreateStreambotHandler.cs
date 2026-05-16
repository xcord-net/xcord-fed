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

namespace Xcord.Features.Streambots;

public sealed record CreateStreambotCommand(
    long ChannelId,
    string Name,
    string Platform,
    string RtmpUrl,
    string StreamKey,
    bool IsDefault
);

public sealed record CreateStreambotRequest(
    string Name,
    string Platform,
    string RtmpUrl,
    string StreamKey,
    bool IsDefault
);

public sealed class CreateStreambotHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    IEncryptionService encryptionService,
    IOptions<TierOptions> tierOptions,
    ILogger<CreateStreambotHandler> logger)
    : IRequestHandler<CreateStreambotCommand, Result<StreamBotDto>>, IValidatable<CreateStreambotCommand>
{
    public Error? Validate(CreateStreambotCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Error.Validation("VALIDATION_ERROR", "Name is required");
        if (request.Name.Length > 64)
            return Error.Validation("VALIDATION_ERROR", "Name must not exceed 64 characters");

        if (!Enum.TryParse<StreamBotPlatform>(request.Platform, ignoreCase: true, out _))
            return Error.Validation("VALIDATION_ERROR", "Platform must be one of: YouTube, Twitch, Rumble, Custom");

        if (string.IsNullOrWhiteSpace(request.RtmpUrl))
            return Error.Validation("VALIDATION_ERROR", "RtmpUrl is required");
        if (!IsValidRtmpUrl(request.RtmpUrl))
            return Error.Validation("VALIDATION_ERROR", "RtmpUrl must be a valid rtmp:// or rtmps:// URL");

        if (string.IsNullOrEmpty(request.StreamKey))
            return Error.Validation("VALIDATION_ERROR", "StreamKey is required");
        if (request.StreamKey.Length > 512)
            return Error.Validation("VALIDATION_ERROR", "StreamKey must not exceed 512 characters");

        return null;
    }

    public async Task<Result<StreamBotDto>> Handle(
        CreateStreambotCommand request, CancellationToken cancellationToken)
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

        var platform = Enum.Parse<StreamBotPlatform>(request.Platform, ignoreCase: true);

        // Enforce tier limit if configured (0 = unlimited)
        var maxPerChannel = tierOptions.Value.MaxStreambotsPerChannel;
        if (maxPerChannel > 0)
        {
            var existingCount = await dbContext.StreamBots
                .CountAsync(s => s.ChannelId == request.ChannelId, cancellationToken);

            if (existingCount >= maxPerChannel)
            {
                return Error.Forbidden(
                    "STREAMBOT_LIMIT_REACHED",
                    $"Channel has reached the maximum of {maxPerChannel} stream bots");
            }
        }

        // If this streambot is marked default, clear the flag on any other defaults
        // for the same channel to maintain a single-default invariant.
        if (request.IsDefault)
        {
            var existingDefaults = await dbContext.StreamBots
                .Where(s => s.ChannelId == request.ChannelId && s.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }
        }

        var now = DateTimeOffset.UtcNow;
        var id = snowflakeGenerator.NextId();

        var streambot = new StreamBot
        {
            Id = id,
            ChannelId = request.ChannelId,
            CreatedByUserId = userId,
            Name = request.Name,
            Platform = platform,
            RtmpUrl = request.RtmpUrl,
            EncryptedStreamKey = encryptionService.Encrypt(request.StreamKey),
            IsDefault = request.IsDefault,
            CreatedAt = now
        };

        dbContext.StreamBots.Add(streambot);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} created streambot {StreamBotId} ({Platform}) in channel {ChannelId}",
            userId, id, platform, request.ChannelId);

        return new StreamBotDto(
            Id: streambot.Id,
            ChannelId: streambot.ChannelId,
            Name: streambot.Name,
            Platform: streambot.Platform,
            RtmpUrl: streambot.RtmpUrl,
            IsDefault: streambot.IsDefault,
            HasStreamKey: streambot.EncryptedStreamKey.Length > 0,
            CreatedAt: streambot.CreatedAt
        );
    }

    internal static bool IsValidRtmpUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var scheme = uri.Scheme.ToLowerInvariant();
        return scheme == "rtmp" || scheme == "rtmps";
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/channels/{channelId:long}/streambots", async (
            long channelId,
            [FromBody] CreateStreambotRequest request,
            [FromServices] CreateStreambotHandler handler,
            CancellationToken ct) =>
        {
            var command = new CreateStreambotCommand(
                ChannelId: channelId,
                Name: request.Name,
                Platform: request.Platform,
                RtmpUrl: request.RtmpUrl,
                StreamKey: request.StreamKey,
                IsDefault: request.IsDefault
            );

            return await handler.ExecuteAsync(
                command, ct,
                success => Results.Created($"/api/v1/streambots/{success.Id}", success));
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateStreambot")
        .WithTags("Streambots");
    }
}
