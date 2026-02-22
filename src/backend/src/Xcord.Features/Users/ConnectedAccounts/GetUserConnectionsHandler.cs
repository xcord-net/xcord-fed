using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Users.ConnectedAccounts;

public sealed record GetUserConnectionsQuery(long UserId);

public sealed class GetUserConnectionsHandler(AppDbContext dbContext)
    : IRequestHandler<GetUserConnectionsQuery, Result<List<ConnectionResponse>>>
{
    public async Task<Result<List<ConnectionResponse>>> Handle(GetUserConnectionsQuery request, CancellationToken ct)
    {
        var connections = await dbContext.ConnectedAccounts.AsNoTracking()
            .Where(c => c.UserId == request.UserId && c.ShowOnProfile)
            .Select(c => new ConnectionResponse(c.Id, c.Provider, c.ProviderUsername, c.IsVerified, c.ShowOnProfile, c.CreatedAt))
            .ToListAsync(ct);
        return connections;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/{userId}/connections", async (
            long userId,
            IRequestHandler<GetUserConnectionsQuery, Result<List<ConnectionResponse>>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetUserConnectionsQuery(userId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("GetUserConnections").WithTags("ConnectedAccounts");
}
