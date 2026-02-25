using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

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
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService,
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
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

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

        // Load the webhook
        var webhook = await dbContext.OutgoingWebhooks
            .FirstOrDefaultAsync(w => w.Id == request.WebhookId && w.ServerId == request.ServerId, cancellationToken);

        if (webhook == null)
            return Error.NotFound("WEBHOOK_NOT_FOUND", "Outgoing webhook not found");

        // Apply partial updates
        if (request.TargetUrl != null)
            webhook.TargetUrl = request.TargetUrl;

        if (request.EventTypes != null)
            webhook.EventTypesJson = JsonSerializer.Serialize(request.EventTypes.Distinct().ToArray());

        if (request.IsActive.HasValue)
            webhook.IsActive = request.IsActive.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

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
            return await handler.ExecuteAsync(command, ct);
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
