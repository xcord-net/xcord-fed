using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Servers;

public sealed record UnboostServerCommand(long ServerId);
public sealed record UnboostResponse(bool Removed);

public sealed class UnboostServerHandler(
    AppDbContext dbContext, ICurrentUserService currentUserService)
    : IRequestHandler<UnboostServerCommand, Result<UnboostResponse>>
{
    public async Task<Result<UnboostResponse>> Handle(UnboostServerCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var boost = await dbContext.ServerBoosts.FirstOrDefaultAsync(
            b => b.ServerId == request.ServerId && b.UserId == userId && b.IsActive, ct);
        if (boost == null) return Error.NotFound("NO_BOOST", "You are not boosting this server");

        boost.IsActive = false;
        boost.DeletedAt = DateTimeOffset.UtcNow;

        var server = await dbContext.Servers.FirstOrDefaultAsync(s => s.Id == request.ServerId, ct);
        if (server != null)
        {
            server.BoostCount = Math.Max(0, server.BoostCount - 1);
            server.BoostLevel = server.BoostCount switch
            {
                >= 14 => 3, >= 7 => 2, >= 2 => 1, _ => 0
            };
        }
        await dbContext.SaveChangesAsync(ct);
        return new UnboostResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/servers/{serverId}/boosts", async (
            long serverId,
            IRequestHandler<UnboostServerCommand, Result<UnboostResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new UnboostServerCommand(serverId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("UnboostServer").WithTags("Boosts");
}
