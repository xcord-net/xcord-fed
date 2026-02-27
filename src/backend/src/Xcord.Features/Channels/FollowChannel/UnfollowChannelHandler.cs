using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

public sealed record UnfollowChannelCommand(long ServerId, long ChannelId, long SubscriptionId);

public sealed class UnfollowChannelHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<UnfollowChannelCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        UnfollowChannelCommand request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Caller must be a member of the source server
        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == request.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_A_MEMBER", "You must be a member of this server");
        }

        var subscription = await dbContext.CrosspostSubscriptions
            .FirstOrDefaultAsync(cs =>
                cs.Id == request.SubscriptionId &&
                cs.SourceChannelId == request.ChannelId &&
                cs.SourceServerId == request.ServerId, cancellationToken);

        if (subscription is null)
        {
            return Error.NotFound("SUBSCRIPTION_NOT_FOUND", "Follow subscription not found");
        }

        // Soft delete
        subscription.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/servers/{serverId:long}/channels/{channelId:long}/followers/{subscriptionId:long}", async (
            long serverId,
            long channelId,
            long subscriptionId,
            UnfollowChannelHandler handler,
            CancellationToken ct) =>
        {
            var command = new UnfollowChannelCommand(serverId, channelId, subscriptionId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("UnfollowChannel")
        .WithTags("Channels");
}
