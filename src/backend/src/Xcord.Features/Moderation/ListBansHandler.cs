using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Moderation;

public sealed record ListBansQuery(
    long ServerId
);

public sealed record BanDto(
    long Id,
    long UserId,
    string Username,
    long? ModeratorId,
    string? Reason,
    DateTimeOffset CreatedAt
);

public sealed class ListBansHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IPermissionService permissionService)
    : IRequestHandler<ListBansQuery, Result<List<BanDto>>>
{
    public async Task<Result<List<BanDto>>> Handle(ListBansQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check if user has BanMembers permission
        var permissionResult = await permissionService.EnsureServerPermission(
            userId,
            request.ServerId,
            Permission.BanMembers);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Get bans for the server (paginated, default limit 100)
        var bans = await dbContext.Bans
            .AsNoTracking()
            .Where(b => b.ServerId == request.ServerId)
            .Include(b => b.User)
            .OrderByDescending(b => b.CreatedAt)
            .Take(100)
            .Select(b => new BanDto(
                b.Id,
                b.UserId,
                b.User.Username,
                b.ModeratorId,
                b.Reason,
                b.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return bans;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/bans", async (
            long serverId,
            IRequestHandler<ListBansQuery, Result<List<BanDto>>> handler,
            CancellationToken ct) =>
        {
            var query = new ListBansQuery(ServerId: serverId);
            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListBans")
        .WithTags("Moderation");
    }
}
