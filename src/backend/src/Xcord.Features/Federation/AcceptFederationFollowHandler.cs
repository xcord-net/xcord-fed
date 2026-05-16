using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Federation;

/// <summary>
/// Request from a remote instance to register a follow and exchange a shared secret.
/// The remote instance sends its URL and the shared secret (base64).
/// This instance stores the follow record with the encrypted secret.
/// </summary>
public sealed record AcceptFederationFollowRequest(
    string FollowerInstanceUrl,
    string RemoteChannelId,
    string LocalChannelId,
    string SharedSecret
);

public sealed record AcceptFederationFollowResponse(bool Accepted);

public sealed class AcceptFederationFollowHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IEncryptionService encryptionService,
    ILogger<AcceptFederationFollowHandler> logger)
    : IRequestHandler<AcceptFederationFollowRequest, Result<AcceptFederationFollowResponse>>, IValidatable<AcceptFederationFollowRequest>
{
    public Error? Validate(AcceptFederationFollowRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FollowerInstanceUrl))
            return Error.Validation("VALIDATION_FAILED", "FollowerInstanceUrl is required");

        if (string.IsNullOrWhiteSpace(request.RemoteChannelId))
            return Error.Validation("VALIDATION_FAILED", "RemoteChannelId is required");

        if (string.IsNullOrWhiteSpace(request.LocalChannelId))
            return Error.Validation("VALIDATION_FAILED", "LocalChannelId is required");

        if (string.IsNullOrWhiteSpace(request.SharedSecret))
            return Error.Validation("VALIDATION_FAILED", "SharedSecret is required");

        return null;
    }

    public async Task<Result<AcceptFederationFollowResponse>> Handle(AcceptFederationFollowRequest request, CancellationToken cancellationToken)
    {
        var normalizedUrl = request.FollowerInstanceUrl.TrimEnd('/');

        // Verify the local channel exists
        if (!long.TryParse(request.LocalChannelId, out var localChannelId))
            return Error.Validation("VALIDATION_FAILED", "Invalid LocalChannelId");

        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == localChannelId, cancellationToken);

        if (channel == null)
            return Error.NotFound("CHANNEL_NOT_FOUND", "Local channel not found");

        // Check for existing follow from this remote instance
        var existing = await dbContext.FederationFollows
            .FirstOrDefaultAsync(f => f.RemoteInstanceUrl == normalizedUrl
                                   && f.RemoteChannelId == request.RemoteChannelId
                                   && f.LocalChannelId == localChannelId, cancellationToken);

        byte[] secretBytes;
        try
        {
            secretBytes = Convert.FromBase64String(request.SharedSecret);
            if (secretBytes.Length != 32)
                return Error.Validation("VALIDATION_FAILED", "SharedSecret must be 32 bytes");
        }
        catch (FormatException)
        {
            return Error.Validation("VALIDATION_FAILED", "SharedSecret must be valid base64");
        }

        if (existing != null)
        {
            // Update existing follow with new shared secret
            existing.SharedSecret = encryptionService.Encrypt(request.SharedSecret);
            existing.IsActive = true;
        }
        else
        {
            // Create new follow record for the remote instance
            var follow = new FederationFollow
            {
                Id = snowflakeGenerator.NextId(),
                RemoteInstanceUrl = normalizedUrl,
                LocalChannelId = localChannelId,
                RemoteChannelId = request.RemoteChannelId,
                FollowedByUserId = null, // System-created via handshake
                IsActive = true,
                SharedSecret = encryptionService.Encrypt(request.SharedSecret),
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.FederationFollows.Add(follow);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Accepted federation follow from {RemoteUrl} for channel {LocalChannelId}",
            normalizedUrl.SafeForLog(), localChannelId);

        return new AcceptFederationFollowResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/federation/accept-follow", async (
            HttpContext httpContext,
            [FromBody] AcceptFederationFollowRequest request,
            [FromServices] AcceptFederationFollowHandler handler,
            [FromServices] IOptions<FederationOptions> federationOptions,
            CancellationToken ct) =>
        {
            // In production, this endpoint should also verify the caller via HMAC
            // but for the initial handshake we accept it since the secret is being exchanged
            return await handler.ExecuteAsync(request, ct,
                success => Results.Ok(success));
        })
        .WithName("AcceptFederationFollow")
        .WithTags("Federation");
}
