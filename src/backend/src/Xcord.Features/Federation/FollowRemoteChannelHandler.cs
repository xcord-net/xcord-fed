using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

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
    IRoleService roleService,
    ICurrentUserService currentUserService,
    IEncryptionService encryptionService,
    ILogger<FollowRemoteChannelHandler> logger)
    : IRequestHandler<FollowRemoteChannelRequest, Result<FederationFollowDto>>, IValidatable<FollowRemoteChannelRequest>
{
    // PostgreSQL unique_violation SQLSTATE.
    private const string UniqueViolationSqlState = "23505";

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
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify local channel exists
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.LocalChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Local channel not found");
        }

        // Check manage channels permission
        var permissionResult = await roleService.EnsureChannelRole(
            userId, channel.Id, Role.ManageChannels);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        var normalizedUrl = request.RemoteInstanceUrl.TrimEnd('/');

        var now = DateTimeOffset.UtcNow;
        var sharedSecret = FederationSignatureService.GenerateSharedSecret();
        var follow = new FederationFollow
        {
            Id = snowflakeGenerator.NextId(),
            RemoteInstanceUrl = normalizedUrl,
            LocalChannelId = request.LocalChannelId,
            RemoteChannelId = request.RemoteChannelId,
            RemoteChannelName = request.RemoteChannelName,
            FollowedByUserId = userId,
            IsActive = true,
            SharedSecret = encryptionService.Encrypt(Convert.ToBase64String(sharedSecret)),
            CreatedAt = now
        };

        dbContext.FederationFollows.Add(follow);

        // Rely on the unique partial index on federation_follows
        // (LocalChannelId, RemoteInstanceUrl, RemoteChannelId) WHERE DeletedAt IS NULL to
        // atomically enforce one-follow-per-remote-channel. This eliminates the TOCTOU window
        // between any prior existence check and the insert. Concurrent duplicate inserts race
        // at SaveChangesAsync; the loser raises a unique_violation that we translate into a
        // clean ALREADY_FOLLOWING conflict.
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsFollowUniqueViolation(ex))
        {
            return Error.Conflict("ALREADY_FOLLOWING", "This remote channel is already being followed");
        }

        logger.LogInformation(
            "User {UserId} created federation follow {FollowId}: {RemoteUrl}/channels/{RemoteChannelId} -> local channel {LocalChannelId}",
            userId, follow.Id, normalizedUrl.SafeForLog(), request.RemoteChannelId.SafeForLog(64), request.LocalChannelId);

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

    private static bool IsFollowUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException pg) return false;
        if (pg.SqlState != UniqueViolationSqlState) return false;
        // ConstraintName is the EF-generated index on
        // (LocalChannelId, RemoteInstanceUrl, RemoteChannelId).
        var name = pg.ConstraintName;
        if (string.IsNullOrEmpty(name)) return false;
        return name.Contains("LocalChannelId", StringComparison.OrdinalIgnoreCase)
            && name.Contains("RemoteInstanceUrl", StringComparison.OrdinalIgnoreCase);
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
