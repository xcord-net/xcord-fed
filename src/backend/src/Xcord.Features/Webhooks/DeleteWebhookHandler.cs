using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Webhooks;

public sealed record DeleteWebhookCommand(
    long ServerId,
    long WebhookId
);

public sealed class DeleteWebhookHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService,
    ILogger<DeleteWebhookHandler> logger)
    : IRequestHandler<DeleteWebhookCommand, Result<bool>>, IValidatable<DeleteWebhookCommand>
{
    public Error? Validate(DeleteWebhookCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be positive");
        }

        if (request.WebhookId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "WebhookId must be positive");
        }

        return null;
    }

    public async Task<Result<bool>> Handle(DeleteWebhookCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Verify server exists
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Get webhook and verify it exists
        var webhook = await dbContext.Webhooks
            .Include(w => w.CreatedByUser)
            .FirstOrDefaultAsync(w => w.Id == request.WebhookId, cancellationToken);

        if (webhook == null)
        {
            return Error.NotFound("WEBHOOK_NOT_FOUND", "Webhook not found");
        }

        // Verify webhook's channel belongs to the server
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == webhook.ChannelId, cancellationToken);

        if (channel == null || channel.ServerId != request.ServerId)
        {
            return Error.NotFound("WEBHOOK_NOT_FOUND", "Webhook not found in this server");
        }

        // Check ManageWebhooks permission
        var permissionResult = await permissionService.EnsureChannelPermission(
            userId,
            webhook.ChannelId,
            Permission.ManageWebhooks);

        if (permissionResult.IsFailure)
        {
            return Error.Forbidden(
                "MISSING_PERMISSIONS",
                "You do not have permission to manage webhooks in this channel");
        }

        // Soft delete webhook
        webhook.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} deleted webhook {WebhookId}",
            userId, request.WebhookId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/webhooks/{webhookId}", async (
            long serverId,
            long webhookId,
            [FromServices] DeleteWebhookHandler handler,
            CancellationToken ct) =>
        {
            var command = new DeleteWebhookCommand(
                ServerId: serverId,
                WebhookId: webhookId
            );

            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteWebhook")
        .WithTags("Webhooks");
    }
}
