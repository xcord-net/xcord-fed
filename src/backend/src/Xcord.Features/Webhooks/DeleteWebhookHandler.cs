using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Features.Moderation;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Webhooks;

public sealed record DeleteWebhookCommand(
    long ServerId,
    long WebhookId
);

public sealed class DeleteWebhookHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
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
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

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
        var permissionResult = await roleService.EnsureChannelRole(
            userId,
            webhook.ChannelId,
            Role.ManageWebhooks);

        if (permissionResult.IsFailure)
        {
            return Error.Forbidden(
                "MISSING_PERMISSIONS",
                "You do not have permission to manage webhooks in this channel");
        }

        // Soft delete webhook
        webhook.SoftDelete();

        // Audit log for webhook deletion (sensitive op affecting external delivery).
        dbContext.AuditLogs.AddEntry(
            snowflakeGenerator,
            request.ServerId,
            userId,
            "WebhookDelete",
            webhook.Id,
            webhook.Name,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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

            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteWebhook")
        .WithTags("Webhooks");
    }
}
