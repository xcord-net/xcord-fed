using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Webhooks;

public sealed record CreateOutgoingWebhookCommand(
    long ServerId,
    string TargetUrl,
    string[] EventTypes
);

public sealed record CreateOutgoingWebhookResponse(
    long Id,
    long ServerId,
    string TargetUrl,
    string[] EventTypes,
    bool IsActive,
    long CreatedByUserId,
    DateTimeOffset CreatedAt,
    string Secret
);

public sealed class CreateOutgoingWebhookHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    IEncryptionService encryptionService,
    ILogger<CreateOutgoingWebhookHandler> logger)
    : IRequestHandler<CreateOutgoingWebhookCommand, Result<CreateOutgoingWebhookResponse>>,
      IValidatable<CreateOutgoingWebhookCommand>
{
    private const int MaxOutgoingWebhooksPerServer = 10;

    public Error? Validate(CreateOutgoingWebhookCommand request)
    {
        if (request.ServerId <= 0)
            return Error.Validation("VALIDATION_ERROR", "ServerId must be positive");

        if (string.IsNullOrWhiteSpace(request.TargetUrl))
            return Error.Validation("VALIDATION_ERROR", "TargetUrl is required");

        if (request.TargetUrl.Length > 2048)
            return Error.Validation("VALIDATION_ERROR", "TargetUrl must be 2048 characters or less");

        if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
            return Error.Validation("VALIDATION_ERROR", "TargetUrl must be a valid HTTP or HTTPS URL");

        if (request.EventTypes == null || request.EventTypes.Length == 0)
            return Error.Validation("VALIDATION_ERROR", "At least one event type is required");

        var invalidTypes = request.EventTypes
            .Where(t => !OutgoingWebhookEventType.All.Contains(t))
            .ToArray();

        if (invalidTypes.Length > 0)
            return Error.Validation("VALIDATION_ERROR",
                $"Invalid event types: {string.Join(", ", invalidTypes)}. Valid types: {string.Join(", ", OutgoingWebhookEventType.All)}");

        return null;
    }

    public async Task<Result<CreateOutgoingWebhookResponse>> Handle(
        CreateOutgoingWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify server exists
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        // Check ManageWebhooks permission
        var permissionResult = await permissionService.EnsureServerPermission(
            userId, request.ServerId, Permission.ManageWebhooks);

        if (permissionResult.IsFailure)
            return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission to manage webhooks in this server");

        // Enforce per-server limit
        var existingCount = await dbContext.OutgoingWebhooks
            .CountAsync(w => w.ServerId == request.ServerId, cancellationToken);

        if (existingCount >= MaxOutgoingWebhooksPerServer)
            return Error.Validation("WEBHOOK_LIMIT_EXCEEDED",
                $"Servers may have at most {MaxOutgoingWebhooksPerServer} outgoing webhooks");

        // Generate a 32-byte random HMAC secret and encrypt it at rest
        var secretBytes = new byte[32];
        RandomNumberGenerator.Fill(secretBytes);
        var secretHex = Convert.ToHexString(secretBytes).ToLowerInvariant();
        var encryptedSecret = encryptionService.Encrypt(secretHex);

        var now = DateTimeOffset.UtcNow;
        var webhookId = snowflakeGenerator.NextId();
        var distinctEventTypes = request.EventTypes.Distinct().ToArray();

        var webhook = new OutgoingWebhook
        {
            Id = webhookId,
            ServerId = request.ServerId,
            TargetUrl = request.TargetUrl,
            Secret = encryptedSecret,
            EventTypesJson = JsonSerializer.Serialize(distinctEventTypes),
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = now
        };

        dbContext.OutgoingWebhooks.Add(webhook);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created outgoing webhook {WebhookId} for server {ServerId}",
            userId, webhookId, request.ServerId);

        // Return the plain-text secret once - it cannot be retrieved again
        return new CreateOutgoingWebhookResponse(
            Id: webhook.Id,
            ServerId: webhook.ServerId,
            TargetUrl: webhook.TargetUrl,
            EventTypes: distinctEventTypes,
            IsActive: webhook.IsActive,
            CreatedByUserId: webhook.CreatedByUserId,
            CreatedAt: webhook.CreatedAt,
            Secret: secretHex
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/outgoing-webhooks", async (
            long serverId,
            CreateOutgoingWebhookRequest request,
            IRequestHandler<CreateOutgoingWebhookCommand, Result<CreateOutgoingWebhookResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateOutgoingWebhookCommand(
                ServerId: serverId,
                TargetUrl: request.TargetUrl,
                EventTypes: request.EventTypes
            );
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAuthorization(Policies.User)
        .WithName("CreateOutgoingWebhook")
        .WithTags("OutgoingWebhooks");
    }
}

public sealed record CreateOutgoingWebhookRequest(
    string TargetUrl,
    string[] EventTypes
);
