using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

public sealed record GetFavoriteChannelsQuery(long ServerId);

public sealed record GetFavoriteChannelsResponse(long[] FavoriteChannelIds);

public sealed class GetFavoriteChannelsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetFavoriteChannelsQuery, Result<GetFavoriteChannelsResponse>>
{
    public async Task<Result<GetFavoriteChannelsResponse>> Handle(GetFavoriteChannelsQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var memberCheck = await dbContext.EnsureMembership(request.ServerId, userId, cancellationToken);
        if (memberCheck.IsFailure) return memberCheck.Error;

        var favoriteIds = await dbContext.ServerMembers
            .Where(sm => sm.UserId == userId && sm.ServerId == request.ServerId && sm.DeletedAt == null)
            .Select(sm => sm.FavoriteChannelIds)
            .FirstOrDefaultAsync(cancellationToken);

        return new GetFavoriteChannelsResponse(favoriteIds ?? Array.Empty<long>());
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId:long}/favorites", async (
            long serverId,
            IRequestHandler<GetFavoriteChannelsQuery, Result<GetFavoriteChannelsResponse>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new GetFavoriteChannelsQuery(serverId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetFavoriteChannels")
        .WithTags("Channels");
    }
}
