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

public sealed record GetBroadcastCommand(long BroadcastId);

public sealed class GetBroadcastHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    BroadcastEgressBuilder egressBuilder)
    : IRequestHandler<GetBroadcastCommand, Result<BroadcastDto>>
{
    public async Task<Result<BroadcastDto>> Handle(
        GetBroadcastCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var broadcast = await dbContext.Broadcasts
            .AsNoTracking()
            .Include(b => b.StageSlots)
            .Include(b => b.ActiveStreambots)
                .ThenInclude(bs => bs.StreamBot)
            .FirstOrDefaultAsync(b => b.Id == request.BroadcastId, cancellationToken);

        if (broadcast == null)
            return Error.NotFound("BROADCAST_NOT_FOUND", "Broadcast not found");

        var permissionResult = await roleService.EnsureChannelRole(
            userId, broadcast.ChannelId, Role.ViewBroadcast);
        if (permissionResult.IsFailure)
            return permissionResult.Error;

        return BuildDto(broadcast, egressBuilder);
    }

    internal static BroadcastDto BuildDto(Broadcast broadcast, BroadcastEgressBuilder egressBuilder)
    {
        return new BroadcastDto(
            Id: broadcast.Id,
            ChannelId: broadcast.ChannelId,
            HostUserId: broadcast.HostUserId,
            LayoutPreset: broadcast.LayoutPreset.ToString(),
            Status: broadcast.Status.ToString(),
            HlsUrl: egressBuilder.BuildHlsUrl(broadcast.Id),
            RoomName: egressBuilder.BuildRoomName(broadcast.ChannelId),
            StartedAt: broadcast.StartedAt,
            EndedAt: broadcast.EndedAt,
            StageSlots: broadcast.StageSlots
                .OrderBy(s => s.SlotIndex)
                .Select(s => new BroadcastStageSlotDto(s.UserId, s.SlotIndex))
                .ToList(),
            Streambots: broadcast.ActiveStreambots
                .Select(bs => new BroadcastStreambotDto(
                    StreambotId: bs.StreamBotId,
                    Name: bs.StreamBot?.Name ?? string.Empty,
                    Status: bs.Status.ToString(),
                    LastError: bs.LastError))
                .ToList());
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/broadcasts/{broadcastId:long}", async (
            long broadcastId,
            [FromServices] GetBroadcastHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new GetBroadcastCommand(broadcastId), ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetBroadcast")
        .WithTags("Broadcasts");
    }
}
