using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Moderation;

public sealed record GetAuditLogQuery(
    long ServerId,
    string? ActionType = null,
    long? ActorId = null,
    int Limit = 50
);

public sealed record AuditLogDto(
    long Id,
    long ServerId,
    long? ActorId,
    string? ActorUsername,
    string ActionType,
    long? TargetId,
    string? Changes,
    string? Reason,
    DateTimeOffset CreatedAt
);

public sealed class GetAuditLogHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService)
    : IRequestHandler<GetAuditLogQuery, Result<List<AuditLogDto>>>
{
    public async Task<Result<List<AuditLogDto>>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Check if user has ManageServer permission
        var permissionResult = await permissionService.EnsureServerPermission(
            userId,
            request.ServerId,
            Permission.ManageServer);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Enforce max limit of 100
        var limit = Math.Min(request.Limit, 100);

        // Build query
        var query = dbContext.AuditLogs
            .AsNoTracking()
            .Where(al => al.ServerId == request.ServerId);

        // Apply optional filters
        if (!string.IsNullOrEmpty(request.ActionType))
        {
            query = query.Where(al => al.ActionType == request.ActionType);
        }

        if (request.ActorId.HasValue)
        {
            query = query.Where(al => al.ActorId == request.ActorId.Value);
        }

        // Get audit logs with actor navigation
        var auditLogs = await query
            .Include(al => al.Actor)
            .OrderByDescending(al => al.CreatedAt)
            .Take(limit)
            .Select(al => new AuditLogDto(
                al.Id,
                al.ServerId,
                al.ActorId,
                al.Actor != null ? al.Actor.Username : null,
                al.ActionType,
                al.TargetId,
                al.Changes,
                al.Reason,
                al.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return auditLogs;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/audit-log", async (
            long serverId,
            string? actionType,
            long? actorId,
            int? limit,
            IRequestHandler<GetAuditLogQuery, Result<List<AuditLogDto>>> handler,
            CancellationToken ct) =>
        {
            var query = new GetAuditLogQuery(
                ServerId: serverId,
                ActionType: actionType,
                ActorId: actorId,
                Limit: limit ?? 50
            );

            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetAuditLog")
        .WithTags("Moderation");
    }
}
