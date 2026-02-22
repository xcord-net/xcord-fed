using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Webhooks;

public sealed record ListWebhooksCommand(
    long ServerId
);

public sealed record WebhookDto(
    long Id,
    long ChannelId,
    string Name,
    string? AvatarUrl,
    long? CreatedByUserId,
    DateTimeOffset CreatedAt
);

public sealed class ListWebhooksHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService,
    ILogger<ListWebhooksHandler> logger)
    : IRequestHandler<ListWebhooksCommand, Result<List<WebhookDto>>>, IValidatable<ListWebhooksCommand>
{
    public Error? Validate(ListWebhooksCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be positive");
        }

        return null;
    }

    public async Task<Result<List<WebhookDto>>> Handle(ListWebhooksCommand request, CancellationToken cancellationToken)
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

        // Check ManageWebhooks permission at server level
        var permissionResult = await permissionService.EnsureServerPermission(
            userId,
            request.ServerId,
            Permission.ManageWebhooks);

        if (permissionResult.IsFailure)
        {
            return Error.Forbidden(
                "MISSING_PERMISSIONS",
                "You do not have permission to manage webhooks in this server");
        }

        // Get all webhooks for channels in this server
        var channelIds = await dbContext.Channels
            .AsNoTracking()
            .Where(c => c.ServerId == request.ServerId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var webhooks = await dbContext.Webhooks
            .AsNoTracking()
            .Where(w => channelIds.Contains(w.ChannelId))
            .Select(w => new WebhookDto(
                w.Id,
                w.ChannelId,
                w.Name,
                w.AvatarUrl,
                w.CreatedByUserId,
                w.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} listed {Count} webhooks for server {ServerId}",
            userId, webhooks.Count, request.ServerId);

        return webhooks;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/webhooks", async (
            long serverId,
            IRequestHandler<ListWebhooksCommand, Result<List<WebhookDto>>> handler,
            CancellationToken ct) =>
        {
            var command = new ListWebhooksCommand(ServerId: serverId);
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListWebhooks")
        .WithTags("Webhooks");
    }
}
