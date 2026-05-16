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
    string? Cursor = null
);

public sealed record ListUserServersResponse(
    List<ServerDto> Servers,
    string? NextCursor = null
);

public sealed class ListUserServersHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ICursorService cursorService)
    : IRequestHandler<ListUserServersQuery, Result<ListUserServersResponse>>
{
    public async Task<Result<ListUserServersResponse>> Handle(ListUserServersQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Decode opaque cursor (returns null when no cursor was supplied)
        var cursorResult = cursorService.Decode(request.Cursor);
        if (cursorResult.IsFailure) return cursorResult.Error;
        var beforeId = cursorResult.Value;

        var limit = Math.Clamp(request.Limit, 1, 200);

        // Get servers where the user is a member, with optional cursor-based pagination
        var query = dbContext.ServerMembers
            .AsNoTracking()
            .Where(sm => sm.UserId == userId)
            .Select(sm => sm.Server);

        if (beforeId.HasValue)
        {
            query = query.Where(s => s.Id < beforeId.Value);
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

        var nextCursor = servers.Count == limit && servers.Count > 0
            ? cursorService.Encode(servers[^1].Id)
            : null;

        return new ListUserServersResponse(Servers: servers, NextCursor: nextCursor);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/users/@me/servers", async (
            IRequestHandler<ListUserServersQuery, Result<ListUserServersResponse>> handler,
            int? limit,
            string? cursor,
            CancellationToken ct) =>
        {
            var query = new ListUserServersQuery(
                Limit: limit ?? 100,
                Cursor: cursor
            );
            return await handler.ExecuteAsync(query, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListUserServers")
        .WithTags("Servers", "Users");
    }
}
