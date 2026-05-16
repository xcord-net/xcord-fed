using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Broadcasts;

public sealed record ListBroadcastsCommand(long ChannelId, bool ActiveOnly);

public sealed class ListActiveBroadcastsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    BroadcastEgressBuilder egressBuilder)
    : IRequestHandler<ListBroadcastsCommand, Result<List<BroadcastDto>>>
{
    public async Task<Result<List<BroadcastDto>>> Handle(
        ListBroadcastsCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var channelExists = await dbContext.Channels
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.ChannelId, cancellationToken);
        if (!channelExists)
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");

        var permissionResult = await roleService.EnsureChannelRole(
            userId, request.ChannelId, Role.ViewBroadcast);
        if (permissionResult.IsFailure)
            return permissionResult.Error;

        var query = dbContext.Broadcasts
            .AsNoTracking()
            .Where(b => b.ChannelId == request.ChannelId)
            .Include(b => b.StageSlots)
            .Include(b => b.ActiveStreambots)
                .ThenInclude(bs => bs.StreamBot)
            .AsQueryable();

        if (request.ActiveOnly)
        {
            query = query.Where(b => b.Status == BroadcastStatus.Starting
                || b.Status == BroadcastStatus.Live);
        }

        var broadcasts = await query
            .OrderByDescending(b => b.StartedAt)
            .ToListAsync(cancellationToken);

        return broadcasts
            .Select(b => GetBroadcastHandler.BuildDto(b, egressBuilder))
            .ToList();
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/channels/{channelId:long}/broadcasts", async (
            long channelId,
            bool? active,
            [FromServices] ListActiveBroadcastsHandler handler,
            CancellationToken ct) =>
        {
            var command = new ListBroadcastsCommand(channelId, ActiveOnly: active == true);
            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListChannelBroadcasts")
        .WithTags("Broadcasts");
    }
}
