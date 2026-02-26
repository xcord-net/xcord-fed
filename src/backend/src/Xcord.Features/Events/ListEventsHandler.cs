using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Events;

public sealed record ListEventsQuery(
    long ServerId,
    EventStatus? Status
);

public sealed class ListEventsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListEventsQuery, Result<List<EventDto>>>
{
    public async Task<Result<List<EventDto>>> Handle(ListEventsQuery request, CancellationToken cancellationToken)
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

        // Build query
        var query = dbContext.ScheduledEvents
            .Where(e => e.ServerId == request.ServerId);

        // Filter by status (default to Scheduled if not provided)
        var status = request.Status ?? EventStatus.Scheduled;
        query = query.Where(e => e.Status == status);

        // Order by scheduled start time (paginated, default limit 100)
        var events = await query
            .OrderBy(e => e.ScheduledStartTime)
            .Take(100)
            .ToListAsync(cancellationToken);

        var eventDtos = events.Select(e => new EventDto(
            Id: e.Id,
            ServerId: e.ServerId,
            CreatorId: e.CreatorId,
            ChannelId: e.ChannelId,
            Name: e.Name,
            Description: e.Description,
            Location: e.Location,
            ImageUrl: e.ImageUrl,
            ScheduledStartTime: e.ScheduledStartTime,
            ScheduledEndTime: e.ScheduledEndTime,
            Status: e.Status,
            InterestedCount: e.InterestedCount,
            CreatedAt: e.CreatedAt
        )).ToList();

        return eventDtos;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/events", async (
            long serverId,
            EventStatus? status,
            IRequestHandler<ListEventsQuery, Result<List<EventDto>>> handler,
            CancellationToken ct) =>
        {
            var query = new ListEventsQuery(serverId, status);
            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListEvents")
        .WithTags("Events");
    }
}
