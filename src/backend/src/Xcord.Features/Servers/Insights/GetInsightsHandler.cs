using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers.Insights;

public sealed record GetInsightsQuery(long ServerId, int Days);
public sealed record InsightDataPoint(DateOnly Date, int TotalMembers, int NewMembers, int MessageCount, int ActiveMembers);
public sealed record InsightsResponse(long ServerId, int TotalMembers, int TotalMessages, List<InsightDataPoint> DataPoints);

public sealed class GetInsightsHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService)
    : IRequestHandler<GetInsightsQuery, Result<InsightsResponse>>
{
    public async Task<Result<InsightsResponse>> Handle(GetInsightsQuery request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var perm = await permissionService.EnsureServerPermission(userId, request.ServerId, Permission.ManageServer);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission");

        var server = await dbContext.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServerId, ct);
        if (server == null) return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        var days = Math.Clamp(request.Days, 1, 90);
        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var channelIds = await dbContext.Channels.AsNoTracking()
            .Where(c => c.ServerId == request.ServerId)
            .Select(c => c.ConversationId)
            .ToListAsync(ct);

        var totalMessages = channelIds.Count > 0
            ? await dbContext.Messages.AsNoTracking()
                .Where(m => channelIds.Contains(m.ConversationId) && m.CreatedAt >= since)
                .CountAsync(ct)
            : 0;

        // Build data points from snapshots or return empty
        var snapshots = await dbContext.ServerInsightSnapshots.AsNoTracking()
            .Where(s => s.ServerId == request.ServerId && s.CreatedAt >= since)
            .OrderBy(s => s.Date)
            .Select(s => new InsightDataPoint(s.Date, s.TotalMembers, s.NewMembers, s.MessageCount, s.ActiveMembers))
            .ToListAsync(ct);

        return new InsightsResponse(server.Id, server.MemberCount, totalMessages, snapshots);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/servers/{serverId}/insights", async (
            long serverId, int? days,
            IRequestHandler<GetInsightsQuery, Result<InsightsResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetInsightsQuery(serverId, days ?? 30), ct))
        .RequireAuthorization(Policies.User)
        .WithName("GetInsights").WithTags("Insights");
}
