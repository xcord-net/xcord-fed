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
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService)
    : IRequestHandler<ListBansQuery, Result<List<BanDto>>>
{
    public async Task<Result<List<BanDto>>> Handle(ListBansQuery request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

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
