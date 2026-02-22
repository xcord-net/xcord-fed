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

public sealed record ListReportsQuery(
    long ServerId,
    ReportStatus? Status = null
);

public sealed record ReportDto(
    long Id,
    long ServerId,
    long ReporterId,
    string ReporterUsername,
    long? ReportedUserId,
    string? ReportedUsername,
    long? ReportedMessageId,
    string Reason,
    ReportStatus Status,
    long? ReviewedById,
    string? ReviewedByUsername,
    string? ReviewNotes,
    DateTimeOffset CreatedAt
);

public sealed class ListReportsHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService)
    : IRequestHandler<ListReportsQuery, Result<List<ReportDto>>>
{
    public async Task<Result<List<ReportDto>>> Handle(ListReportsQuery request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Check if user has ManageReports permission
        var permissionResult = await permissionService.EnsureServerPermission(
            userId,
            request.ServerId,
            Permission.ManageReports);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Build query
        var query = dbContext.Reports
            .AsNoTracking()
            .Where(r => r.ServerId == request.ServerId && r.DeletedAt == null);

        // Apply status filter if provided
        if (request.Status.HasValue)
        {
            query = query.Where(r => r.Status == request.Status.Value);
        }

        // Get reports with navigation properties (paginated, default limit 100)
        var reports = await query
            .Include(r => r.Reporter)
            .Include(r => r.ReportedUser)
            .Include(r => r.ReviewedBy)
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .Select(r => new ReportDto(
                r.Id,
                r.ServerId,
                r.ReporterId,
                r.Reporter.Username,
                r.ReportedUserId,
                r.ReportedUser != null ? r.ReportedUser.Username : null,
                r.ReportedMessageId,
                r.Reason,
                r.Status,
                r.ReviewedById,
                r.ReviewedBy != null ? r.ReviewedBy.Username : null,
                r.ReviewNotes,
                r.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return reports;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/reports", async (
            long serverId,
            ReportStatus? status,
            IRequestHandler<ListReportsQuery, Result<List<ReportDto>>> handler,
            CancellationToken ct) =>
        {
            var query = new ListReportsQuery(
                ServerId: serverId,
                Status: status
            );

            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListReports")
        .WithTags("Moderation");
    }
}
