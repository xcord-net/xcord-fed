using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Features.Moderation;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Webhooks;

public sealed record UpdateOutgoingWebhookCommand(
    long ServerId,
    long WebhookId,
    string? TargetUrl,
    string[]? EventTypes,
    bool? IsActive
);

public sealed class UpdateOutgoingWebhookHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<UpdateOutgoingWebhookHandler> logger)
    : IRequestHandler<UpdateOutgoingWebhookCommand, Result<OutgoingWebhookDto>>,
      IValidatable<UpdateOutgoingWebhookCommand>
{
    public Error? Validate(UpdateOutgoingWebhookCommand request)
    {
        if (request.ServerId <= 0)
            return Error.Validation("VALIDATION_ERROR", "ServerId must be positive");

        if (request.WebhookId <= 0)
            return Error.Validation("VALIDATION_ERROR", "WebhookId must be positive");

        if (request.TargetUrl != null)
        {
            if (request.TargetUrl.Length > 2048)
                return Error.Validation("VALIDATION_ERROR", "TargetUrl must be 2048 characters or less");

            if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http"))
                return Error.Validation("VALIDATION_ERROR", "TargetUrl must be a valid HTTP or HTTPS URL");
        }

        if (request.EventTypes != null)
        {
            if (request.EventTypes.Length == 0)
                return Error.Validation("VALIDATION_ERROR", "At least one event type is required");

            var invalidTypes = request.EventTypes
                .Where(t => !OutgoingWebhookEventType.All.Contains(t))
                .ToArray();

            if (invalidTypes.Length > 0)
                return Error.Validation("VALIDATION_ERROR",
                    $"Invalid event types: {string.Join(", ", invalidTypes)}");
        }

        return null;
    }

    public async Task<Result<OutgoingWebhookDto>> Handle(
        UpdateOutgoingWebhookCommand request,
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
        var permissionResult = await roleService.EnsureServerRole(
            userId, request.ServerId, Role.ManageWebhooks);

        if (permissionResult.IsFailure)
            return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission to manage webhooks in this server");

        // Load the webhook
        var webhook = await dbContext.OutgoingWebhooks
            .FirstOrDefaultAsync(w => w.Id == request.WebhookId && w.ServerId == request.ServerId, cancellationToken);

        if (webhook == null)
            return Error.NotFound("WEBHOOK_NOT_FOUND", "Outgoing webhook not found");

        // Capture before-state so the audit log can record whether the URL changed
        // without ever persisting the URL itself (only its SHA256 hash).
        var previousUrl = webhook.TargetUrl;
        var previousIsActive = webhook.IsActive;
        var previousEventTypesJson = webhook.EventTypesJson;

        // Apply partial updates
        if (request.TargetUrl != null)
            webhook.TargetUrl = request.TargetUrl;

        if (request.EventTypes != null)
            webhook.EventTypesJson = JsonSerializer.Serialize(request.EventTypes.Distinct().ToArray());

        if (request.IsActive.HasValue)
            webhook.IsActive = request.IsActive.Value;

        // Audit log for outgoing webhook update. Forensic note: PreviousUrlHash is a
        // SHA256 hex digest of the prior TargetUrl (never the URL itself) so an
        // operator can compare suspicious URLs against historical state without
        // leaking the URL into the audit table.
        var urlChanged = request.TargetUrl != null && !string.Equals(previousUrl, webhook.TargetUrl, StringComparison.Ordinal);
        var changes = new
        {
            UrlChanged = urlChanged,
            PreviousUrlHash = urlChanged ? UpdateOutgoingWebhookHashing.Sha256Hex(previousUrl) : null,
            IsActiveChanged = request.IsActive.HasValue && previousIsActive != webhook.IsActive,
            EventTypesChanged = request.EventTypes != null && !string.Equals(previousEventTypesJson, webhook.EventTypesJson, StringComparison.Ordinal)
        };

        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = snowflakeGenerator.NextId(),
            ServerId = request.ServerId,
            ActorId = userId,
            ActionType = "WebhookUpdate",
            TargetId = webhook.Id,
            Changes = JsonSerializer.Serialize(changes),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} updated outgoing webhook {WebhookId} for server {ServerId}",
            userId, request.WebhookId, request.ServerId);

        return new OutgoingWebhookDto(
            Id: webhook.Id,
            ServerId: webhook.ServerId,
            TargetUrl: webhook.TargetUrl,
            EventTypes: JsonSerializer.Deserialize<string[]>(webhook.EventTypesJson) ?? [],
            IsActive: webhook.IsActive,
            CreatedByUserId: webhook.CreatedByUserId,
            CreatedAt: webhook.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/servers/{serverId}/outgoing-webhooks/{webhookId}", async (
            long serverId,
            long webhookId,
            UpdateOutgoingWebhookRequest request,
            IRequestHandler<UpdateOutgoingWebhookCommand, Result<OutgoingWebhookDto>> handler,
            CancellationToken ct) =>
        {
            var command = new UpdateOutgoingWebhookCommand(
                ServerId: serverId,
                WebhookId: webhookId,
                TargetUrl: request.TargetUrl,
                EventTypes: request.EventTypes,
                IsActive: request.IsActive
            );
            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAuthorization(Policies.User)
        .WithName("UpdateOutgoingWebhook")
        .WithTags("OutgoingWebhooks");
    }
}

public sealed record UpdateOutgoingWebhookRequest(
    string? TargetUrl,
    string[]? EventTypes,
    bool? IsActive
);

internal static class UpdateOutgoingWebhookHashing
{
    public static string Sha256Hex(string? value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}
