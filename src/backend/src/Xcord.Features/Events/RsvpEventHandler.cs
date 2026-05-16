using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Events;

public sealed record RsvpEventCommand(
    long ServerId,
    long EventId
);

public sealed class RsvpEventHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<RsvpEventHandler> logger)
    : IRequestHandler<RsvpEventCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RsvpEventCommand request, CancellationToken cancellationToken)
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

        // Verify event exists
        var scheduledEvent = await dbContext.ScheduledEvents
            .FirstOrDefaultAsync(e => e.Id == request.EventId && e.ServerId == request.ServerId, cancellationToken);

        if (scheduledEvent == null)
        {
            return Error.NotFound("EVENT_NOT_FOUND", "Event not found");
        }

        // Check if RSVP already exists
        var existingRsvp = await dbContext.EventRsvps
            .FirstOrDefaultAsync(r => r.EventId == request.EventId && r.UserId == userId, cancellationToken);

        if (existingRsvp != null)
        {
            // Already RSVP'd, return success
            return true;
        }

        // Create RSVP
        var rsvp = new EventRsvp
        {
            EventId = request.EventId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.EventRsvps.Add(rsvp);

        // Increment interested count
        scheduledEvent.InterestedCount++;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} RSVP'd to event {EventId} in server {ServerId}",
            userId, request.EventId, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPut("/api/v1/servers/{serverId}/events/{eventId}/rsvp", async (
            long serverId,
            long eventId,
            IRequestHandler<RsvpEventCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new RsvpEventCommand(serverId, eventId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("RsvpEvent")
        .WithTags("Events");
    }
}
