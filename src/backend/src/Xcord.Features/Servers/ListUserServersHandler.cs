using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers;

public sealed record ListUserServersQuery(
    int Limit = 100,
    long? Before = null
);

public sealed class ListUserServersHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListUserServersQuery, Result<List<ServerDto>>>
{
    public async Task<Result<List<ServerDto>>> Handle(ListUserServersQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var limit = Math.Clamp(request.Limit, 1, 200);

        // Get servers where the user is a member, with optional cursor-based pagination
        var query = dbContext.ServerMembers
            .AsNoTracking()
            .Where(sm => sm.UserId == userId)
            .Select(sm => sm.Server);

        if (request.Before.HasValue)
        {
            query = query.Where(s => s.Id < request.Before.Value);
        }

        var servers = await query
            .OrderByDescending(s => s.Id)
            .Take(limit)
            .Select(s => new ServerDto(
                s.Id,
                s.Name,
                s.Description,
                s.IconUrl,
                s.BannerUrl,
                s.OwnerId,
                s.MemberCount,
                s.PreferredLocale,
                s.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return servers;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/users/@me/servers", async (
            IRequestHandler<ListUserServersQuery, Result<List<ServerDto>>> handler,
            int? limit,
            long? before,
            CancellationToken ct) =>
        {
            var query = new ListUserServersQuery(
                Limit: limit ?? 100,
                Before: before
            );
            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListUserServers")
        .WithTags("Servers", "Users");
    }
}
