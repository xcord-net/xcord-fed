using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

public sealed record ListFollowersCommand(long ServerId, long ChannelId);

public sealed record FollowerDto(
    long Id,
    long TargetChannelId,
    long TargetServerId,
    string TargetChannelName,
    DateTimeOffset CreatedAt
);

public sealed class ListFollowersHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListFollowersCommand, Result<List<FollowerDto>>>
{
    public async Task<Result<List<FollowerDto>>> Handle(
        ListFollowersCommand request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Caller must be a member of the source server
        var memberCheck = await dbContext.EnsureMembership(request.ServerId, userId, cancellationToken);
        if (memberCheck.IsFailure) return memberCheck.Error;

        // Check channel exists and belongs to server
        var channelExists = await dbContext.Channels
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.ChannelId && c.ServerId == request.ServerId, cancellationToken);

        if (!channelExists)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        var followers = await dbContext.CrosspostSubscriptions
            .AsNoTracking()
            .Where(cs => cs.SourceChannelId == request.ChannelId)
            .Include(cs => cs.TargetChannel)
            .Select(cs => new FollowerDto(
                cs.Id,
                cs.TargetChannelId,
                cs.TargetServerId,
                cs.TargetChannel.Name,
                cs.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return followers;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/servers/{serverId:long}/channels/{channelId:long}/followers", async (
            long serverId,
            long channelId,
            ListFollowersHandler handler,
            CancellationToken ct) =>
        {
            var command = new ListFollowersCommand(serverId, channelId);
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("ListChannelFollowers")
        .WithTags("Channels");
}
