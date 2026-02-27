using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Servers;

public sealed record BoostServerCommand(long ServerId);
public sealed record BoostResponse(long ServerId, int BoostCount, int BoostLevel, List<BoosterInfo> Boosters);
public sealed record BoosterInfo(long UserId, DateTimeOffset StartedAt);

public sealed class BoostServerHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService)
    : IRequestHandler<BoostServerCommand, Result<BoostResponse>>
{
    public async Task<Result<BoostResponse>> Handle(BoostServerCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var server = await dbContext.Servers.FirstOrDefaultAsync(s => s.Id == request.ServerId, ct);
        if (server == null) return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        var existing = await dbContext.ServerBoosts.AsNoTracking()
            .AnyAsync(b => b.ServerId == request.ServerId && b.UserId == userId && b.IsActive, ct);
        if (existing) return Error.Conflict("ALREADY_BOOSTED", "You are already boosting this server");

        var now = DateTimeOffset.UtcNow;
        var boost = new ServerBoost
        {
            Id = snowflakeGenerator.NextId(), ServerId = request.ServerId, UserId = userId,
            StartedAt = now, IsActive = true, CreatedAt = now
        };
        dbContext.ServerBoosts.Add(boost);

        server.BoostCount++;
        server.BoostLevel = server.BoostCount switch
        {
            >= 14 => 3, >= 7 => 2, >= 2 => 1, _ => 0
        };
        await dbContext.SaveChangesAsync(ct);

        var boosters = await dbContext.ServerBoosts.AsNoTracking()
            .Where(b => b.ServerId == request.ServerId && b.IsActive)
            .Select(b => new BoosterInfo(b.UserId, b.StartedAt))
            .ToListAsync(ct);

        return new BoostResponse(server.Id, server.BoostCount, server.BoostLevel, boosters);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/servers/{serverId}/boosts", async (
            long serverId,
            IRequestHandler<BoostServerCommand, Result<BoostResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new BoostServerCommand(serverId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("BoostServer").WithTags("Boosts");
}
