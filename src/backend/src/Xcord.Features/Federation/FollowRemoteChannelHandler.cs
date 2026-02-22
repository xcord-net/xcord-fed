using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Federation;

public sealed record FollowRemoteChannelRequest(
    string RemoteInstanceUrl,
    string RemoteChannelId,
    long LocalChannelId,
    string? RemoteChannelName
);

public sealed class FollowRemoteChannelHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IPermissionService permissionService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<FollowRemoteChannelHandler> logger)
    : IRequestHandler<FollowRemoteChannelRequest, Result<FederationFollowDto>>, IValidatable<FollowRemoteChannelRequest>
{
    public Error? Validate(FollowRemoteChannelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RemoteInstanceUrl))
            return Error.Validation("VALIDATION_FAILED", "RemoteInstanceUrl is required");

        if (!Uri.TryCreate(request.RemoteInstanceUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
            return Error.Validation("VALIDATION_FAILED", "RemoteInstanceUrl must be a valid HTTP(S) URL");

        if (string.IsNullOrWhiteSpace(request.RemoteChannelId))
            return Error.Validation("VALIDATION_FAILED", "RemoteChannelId is required");

        return null;
    }

    public async Task<Result<FederationFollowDto>> Handle(FollowRemoteChannelRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Verify local channel exists
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.LocalChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Local channel not found");
        }

        // Check manage channels permission
        var permissionResult = await permissionService.EnsureChannelPermission(
            userId, channel.Id, Permission.ManageChannels);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Check for duplicate follow
        var normalizedUrl = request.RemoteInstanceUrl.TrimEnd('/');
        var existing = await dbContext.FederationFollows
            .AsNoTracking()
            .AnyAsync(f => f.LocalChannelId == request.LocalChannelId
                        && f.RemoteInstanceUrl == normalizedUrl
                        && f.RemoteChannelId == request.RemoteChannelId,
                cancellationToken);

        if (existing)
        {
            return Error.Conflict("ALREADY_FOLLOWING", "This remote channel is already being followed");
        }

        var now = DateTimeOffset.UtcNow;
        var follow = new FederationFollow
        {
            Id = snowflakeGenerator.NextId(),
            RemoteInstanceUrl = normalizedUrl,
            LocalChannelId = request.LocalChannelId,
            RemoteChannelId = request.RemoteChannelId,
            RemoteChannelName = request.RemoteChannelName,
            FollowedByUserId = userId,
            IsActive = true,
            CreatedAt = now
        };

        dbContext.FederationFollows.Add(follow);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created federation follow {FollowId}: {RemoteUrl}/channels/{RemoteChannelId} -> local channel {LocalChannelId}",
            userId, follow.Id, normalizedUrl, request.RemoteChannelId, request.LocalChannelId);

        return new FederationFollowDto(
            follow.Id,
            follow.RemoteInstanceUrl,
            follow.LocalChannelId,
            follow.RemoteChannelId,
            follow.RemoteChannelName,
            follow.FollowedByUserId,
            follow.IsActive,
            follow.CreatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/federation/follows", async (
            [FromBody] FollowRemoteChannelRequest request,
            [FromServices] FollowRemoteChannelHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(request, ct,
                success => Results.Created($"/api/v1/federation/follows/{success.Id}", success));
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("FollowRemoteChannel")
        .WithTags("Federation");
}
