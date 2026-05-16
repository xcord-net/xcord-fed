using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Events;

public sealed record GetEventQuery(
    long ServerId,
    long EventId
);

public sealed class GetEventHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetEventQuery, Result<EventDto>>
{
    public async Task<Result<EventDto>> Handle(GetEventQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify user is a member of the server
        var isMember = await dbContext.ServerMembers
            .AnyAsync(sm => sm.ServerId == request.ServerId && sm.UserId == userId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_MEMBER", "You are not a member of this server");
        }

        // Get the event
        var scheduledEvent = await dbContext.ScheduledEvents
            .FirstOrDefaultAsync(e => e.Id == request.EventId && e.ServerId == request.ServerId, cancellationToken);

        if (scheduledEvent == null)
        {
            return Error.NotFound("EVENT_NOT_FOUND", "Event not found");
        }

        return new EventDto(
            Id: scheduledEvent.Id,
            ServerId: scheduledEvent.ServerId,
            CreatorId: scheduledEvent.CreatorId,
            ChannelId: scheduledEvent.ChannelId,
            Name: scheduledEvent.Name,
            Description: scheduledEvent.Description,
            Location: scheduledEvent.Location,
            ImageUrl: scheduledEvent.ImageUrl,
            ScheduledStartTime: scheduledEvent.ScheduledStartTime,
            ScheduledEndTime: scheduledEvent.ScheduledEndTime,
            Status: scheduledEvent.Status,
            InterestedCount: scheduledEvent.InterestedCount,
            CreatedAt: scheduledEvent.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/events/{eventId}", async (
            long serverId,
            long eventId,
            IRequestHandler<GetEventQuery, Result<EventDto>> handler,
            CancellationToken ct) =>
        {
            var query = new GetEventQuery(serverId, eventId);
            return await handler.ExecuteAsync(query, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetEvent")
        .WithTags("Events");
    }
}
