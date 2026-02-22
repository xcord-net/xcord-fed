using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Servers.Boosts;

public sealed record GetBoostStatusQuery(long ServerId);

public sealed class GetBoostStatusHandler(AppDbContext dbContext)
    : IRequestHandler<GetBoostStatusQuery, Result<BoostResponse>>
{
    public async Task<Result<BoostResponse>> Handle(GetBoostStatusQuery request, CancellationToken ct)
    {
        var server = await dbContext.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServerId, ct);
        if (server == null) return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        var boosters = await dbContext.ServerBoosts.AsNoTracking()
            .Where(b => b.ServerId == request.ServerId && b.IsActive)
            .Select(b => new BoosterInfo(b.UserId, b.StartedAt))
            .ToListAsync(ct);

        return new BoostResponse(server.Id, server.BoostCount, server.BoostLevel, boosters);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/servers/{serverId}/boosts", async (
            long serverId,
            IRequestHandler<GetBoostStatusQuery, Result<BoostResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetBoostStatusQuery(serverId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("GetBoostStatus").WithTags("Boosts");
}
