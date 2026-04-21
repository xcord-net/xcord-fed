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

namespace Xcord.Features.Streambots;

public sealed record UpdateStreambotCommand(
    long Id,
    string? Name,
    string? RtmpUrl,
    string? StreamKey,
    bool? IsDefault
);

public sealed record UpdateStreambotRequest(
    string? Name,
    string? RtmpUrl,
    string? StreamKey,
    bool? IsDefault
);

public sealed class UpdateStreambotHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    IEncryptionService encryptionService,
    ILogger<UpdateStreambotHandler> logger)
    : IRequestHandler<UpdateStreambotCommand, Result<StreamBotDto>>, IValidatable<UpdateStreambotCommand>
{
    public Error? Validate(UpdateStreambotCommand request)
    {
        if (request.Name != null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Error.Validation("VALIDATION_ERROR", "Name cannot be empty");
            if (request.Name.Length > 64)
                return Error.Validation("VALIDATION_ERROR", "Name must not exceed 64 characters");
        }

        if (request.RtmpUrl != null)
        {
            if (string.IsNullOrWhiteSpace(request.RtmpUrl))
                return Error.Validation("VALIDATION_ERROR", "RtmpUrl cannot be empty");
            if (!CreateStreambotHandler.IsValidRtmpUrl(request.RtmpUrl))
                return Error.Validation("VALIDATION_ERROR", "RtmpUrl must be a valid rtmp:// or rtmps:// URL");
        }

        if (request.StreamKey != null)
        {
            if (request.StreamKey.Length == 0)
                return Error.Validation("VALIDATION_ERROR", "StreamKey cannot be empty");
            if (request.StreamKey.Length > 512)
                return Error.Validation("VALIDATION_ERROR", "StreamKey must not exceed 512 characters");
        }

        return null;
    }

    public async Task<Result<StreamBotDto>> Handle(
        UpdateStreambotCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var streambot = await dbContext.StreamBots
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (streambot == null)
            return Error.NotFound("STREAMBOT_NOT_FOUND", "Stream bot not found");

        // Require ManageBroadcasts on the owning channel
        var permissionResult = await roleService.EnsureChannelRole(
            userId, streambot.ChannelId, Role.ManageBroadcasts);

        if (permissionResult.IsFailure)
            return permissionResult.Error;

        // Apply only supplied fields
        if (request.Name != null)
            streambot.Name = request.Name;

        if (request.RtmpUrl != null)
            streambot.RtmpUrl = request.RtmpUrl;

        if (request.StreamKey != null)
            streambot.EncryptedStreamKey = encryptionService.Encrypt(request.StreamKey);

        if (request.IsDefault.HasValue)
        {
            // When promoting this bot to default, demote any other default bots on the same channel
            if (request.IsDefault.Value && !streambot.IsDefault)
            {
                var otherDefaults = await dbContext.StreamBots
                    .Where(s => s.ChannelId == streambot.ChannelId
                        && s.IsDefault
                        && s.Id != streambot.Id)
                    .ToListAsync(cancellationToken);

                foreach (var other in otherDefaults)
                {
                    other.IsDefault = false;
                }
            }

            streambot.IsDefault = request.IsDefault.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} updated streambot {StreamBotId}",
            userId, streambot.Id);

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

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/streambots/{id:long}", async (
            long id,
            [FromBody] UpdateStreambotRequest request,
            [FromServices] UpdateStreambotHandler handler,
            CancellationToken ct) =>
        {
            var command = new UpdateStreambotCommand(
                Id: id,
                Name: request.Name,
                RtmpUrl: request.RtmpUrl,
                StreamKey: request.StreamKey,
                IsDefault: request.IsDefault
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateStreambot")
        .WithTags("Streambots");
    }
}
