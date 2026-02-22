using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Users.ConnectedAccounts;

public sealed record ListConnectionsQuery;
public sealed record ConnectionResponse(long Id, string Provider, string? ProviderUsername, bool IsVerified, bool ShowOnProfile, DateTimeOffset CreatedAt);

public sealed class ListConnectedAccountsHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<ListConnectionsQuery, Result<List<ConnectionResponse>>>
{
    public async Task<Result<List<ConnectionResponse>>> Handle(ListConnectionsQuery request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var connections = await dbContext.ConnectedAccounts.AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => new ConnectionResponse(c.Id, c.Provider, c.ProviderUsername, c.IsVerified, c.ShowOnProfile, c.CreatedAt))
            .ToListAsync(ct);
        return connections;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/@me/connections", async (
            IRequestHandler<ListConnectionsQuery, Result<List<ConnectionResponse>>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ListConnectionsQuery(), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ListConnections").WithTags("ConnectedAccounts");
}
