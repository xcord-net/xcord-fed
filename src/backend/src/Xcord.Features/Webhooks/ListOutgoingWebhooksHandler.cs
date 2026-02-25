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

public sealed record ListOutgoingWebhooksCommand(long ServerId);

public sealed record OutgoingWebhookDto(
    long Id,
    long ServerId,
    string TargetUrl,
    string[] EventTypes,
    bool IsActive,
    long CreatedByUserId,
    DateTimeOffset CreatedAt
);

public sealed class ListOutgoingWebhooksHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService,
    ILogger<ListOutgoingWebhooksHandler> logger)
    : IRequestHandler<ListOutgoingWebhooksCommand, Result<List<OutgoingWebhookDto>>>,
      IValidatable<ListOutgoingWebhooksCommand>
{
    public Error? Validate(ListOutgoingWebhooksCommand request)
    {
        if (request.ServerId <= 0)
            return Error.Validation("VALIDATION_ERROR", "ServerId must be positive");

        return null;
    }

    public async Task<Result<List<OutgoingWebhookDto>>> Handle(
        ListOutgoingWebhooksCommand request,
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

        var webhooks = await dbContext.OutgoingWebhooks
            .AsNoTracking()
            .Where(w => w.ServerId == request.ServerId)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} listed {Count} outgoing webhooks for server {ServerId}",
            userId, webhooks.Count, request.ServerId);

        return webhooks.Select(w => new OutgoingWebhookDto(
            Id: w.Id,
            ServerId: w.ServerId,
            TargetUrl: w.TargetUrl,
            EventTypes: JsonSerializer.Deserialize<string[]>(w.EventTypesJson) ?? [],
            IsActive: w.IsActive,
            CreatedByUserId: w.CreatedByUserId,
            CreatedAt: w.CreatedAt
        )).ToList();
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/outgoing-webhooks", async (
            long serverId,
            IRequestHandler<ListOutgoingWebhooksCommand, Result<List<OutgoingWebhookDto>>> handler,
            CancellationToken ct) =>
        {
            var command = new ListOutgoingWebhooksCommand(ServerId: serverId);
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAuthorization(Policies.User)
        .WithName("ListOutgoingWebhooks")
        .WithTags("OutgoingWebhooks");
    }
}
