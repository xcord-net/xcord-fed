using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

public sealed record UpdateFavoriteChannelsCommand(long ServerId, long[] FavoriteChannelIds);

public sealed class UpdateFavoriteChannelsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateFavoriteChannelsCommand, Result<GetFavoriteChannelsResponse>>
{
    public async Task<Result<GetFavoriteChannelsResponse>> Handle(UpdateFavoriteChannelsCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var member = await dbContext.ServerMembers
            .FirstOrDefaultAsync(sm => sm.UserId == userId && sm.ServerId == request.ServerId && sm.DeletedAt == null, cancellationToken);

        if (member is null)
            return Error.NotFound("NOT_A_MEMBER", "You are not a member of this server");

        member.FavoriteChannelIds = request.FavoriteChannelIds;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new GetFavoriteChannelsResponse(member.FavoriteChannelIds);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPut("/api/v1/servers/{serverId:long}/favorites", async (
            long serverId,
            [FromBody] UpdateFavoriteChannelsRequest body,
            IRequestHandler<UpdateFavoriteChannelsCommand, Result<GetFavoriteChannelsResponse>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(
                new UpdateFavoriteChannelsCommand(serverId, body.FavoriteChannelIds), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateFavoriteChannels")
        .WithTags("Channels");
    }
}

public sealed record UpdateFavoriteChannelsRequest(long[] FavoriteChannelIds);
