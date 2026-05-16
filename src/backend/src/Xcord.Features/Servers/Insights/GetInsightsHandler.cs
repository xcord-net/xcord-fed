using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Servers;

public sealed record GetInsightsQuery(long ServerId, int Days);
public sealed record InsightDataPoint(DateOnly Date, int TotalMembers, int NewMembers, int MessageCount, int ActiveMembers);
public sealed record InsightsResponse(long ServerId, int TotalMembers, int TotalMessages, List<InsightDataPoint> DataPoints);

public sealed class GetInsightsHandler(
    AppDbContext dbContext, ICurrentUserService currentUserService,
    IRoleService roleService)
    : IRequestHandler<GetInsightsQuery, Result<InsightsResponse>>
{
    public async Task<Result<InsightsResponse>> Handle(GetInsightsQuery request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var perm = await roleService.EnsureServerRole(userId, request.ServerId, Role.ManageServer).ConfigureAwait(false);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission");

        var server = await dbContext.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServerId, ct).ConfigureAwait(false);
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
