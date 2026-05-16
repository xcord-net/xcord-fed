using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Webhooks;

public sealed record DeleteOutgoingWebhookCommand(
    long ServerId,
    long WebhookId
);

public sealed class DeleteOutgoingWebhookHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<DeleteOutgoingWebhookHandler> logger)
    : IRequestHandler<DeleteOutgoingWebhookCommand, Result<bool>>,
      IValidatable<DeleteOutgoingWebhookCommand>
{
    public Error? Validate(DeleteOutgoingWebhookCommand request)
    {
        if (request.ServerId <= 0)
            return Error.Validation("VALIDATION_ERROR", "ServerId must be positive");

        if (request.WebhookId <= 0)
            return Error.Validation("VALIDATION_ERROR", "WebhookId must be positive");

        return null;
    }

    public async Task<Result<bool>> Handle(
        DeleteOutgoingWebhookCommand request,
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

        // Soft delete
        webhook.SoftDelete();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} deleted outgoing webhook {WebhookId} for server {ServerId}",
            userId, request.WebhookId, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/outgoing-webhooks/{webhookId}", async (
            long serverId,
            long webhookId,
            IRequestHandler<DeleteOutgoingWebhookCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new DeleteOutgoingWebhookCommand(
                ServerId: serverId,
                WebhookId: webhookId
            );
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAuthorization(Policies.User)
        .WithName("DeleteOutgoingWebhook")
        .WithTags("OutgoingWebhooks");
    }
}
