using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Servers.Insights;

public sealed record GetInsightsSummaryQuery(long ServerId);
public sealed record InsightsSummaryResponse(long ServerId, int TotalMembers, int MembersToday, int MessagesToday, int ActiveMembersToday);

public sealed class GetInsightsSummaryHandler(
    AppDbContext dbContext, ICurrentUserService currentUserService,
    IPermissionService permissionService)
    : IRequestHandler<GetInsightsSummaryQuery, Result<InsightsSummaryResponse>>
{
    public async Task<Result<InsightsSummaryResponse>> Handle(GetInsightsSummaryQuery request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var perm = await permissionService.EnsureServerPermission(userId, request.ServerId, Permission.ManageServer);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission");

        var server = await dbContext.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServerId, ct);
        if (server == null) return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        var todayStart = DateTimeOffset.UtcNow.Date;
        var todayOffset = new DateTimeOffset(todayStart, TimeSpan.Zero);

        var membersToday = await dbContext.ServerMembers.AsNoTracking()
            .CountAsync(m => m.ServerId == request.ServerId && m.JoinedAt >= todayOffset, ct);

        var channelIds = await dbContext.Channels.AsNoTracking()
            .Where(c => c.ServerId == request.ServerId)
            .Select(c => c.ConversationId)
            .ToListAsync(ct);

        var messagesToday = channelIds.Count > 0
            ? await dbContext.Messages.AsNoTracking()
                .CountAsync(m => channelIds.Contains(m.ConversationId) && m.CreatedAt >= todayOffset, ct)
            : 0;

        var activeToday = channelIds.Count > 0
            ? await dbContext.Messages.AsNoTracking()
                .Where(m => channelIds.Contains(m.ConversationId) && m.CreatedAt >= todayOffset && m.AuthorId != null)
                .Select(m => m.AuthorId)
                .Distinct()
                .CountAsync(ct)
            : 0;

        return new InsightsSummaryResponse(server.Id, server.MemberCount, membersToday, messagesToday, activeToday);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/servers/{serverId}/insights/summary", async (
            long serverId,
            IRequestHandler<GetInsightsSummaryQuery, Result<InsightsSummaryResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetInsightsSummaryQuery(serverId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("GetInsightsSummary").WithTags("Insights");
}
