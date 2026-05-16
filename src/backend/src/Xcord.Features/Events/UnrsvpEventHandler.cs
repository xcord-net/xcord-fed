using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Events;

public sealed record UnrsvpEventCommand(
    long ServerId,
    long EventId
);

public sealed class UnrsvpEventHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<UnrsvpEventHandler> logger)
    : IRequestHandler<UnrsvpEventCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UnrsvpEventCommand request, CancellationToken cancellationToken)
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

        // Get the RSVP
        var rsvp = await dbContext.EventRsvps
            .FirstOrDefaultAsync(r => r.EventId == request.EventId && r.UserId == userId, cancellationToken);

        if (rsvp == null)
        {
            // No RSVP exists, return success
            return true;
        }

        // Hard delete the RSVP
        dbContext.EventRsvps.Remove(rsvp);

        // Decrement interested count
        if (scheduledEvent.InterestedCount > 0)
        {
            scheduledEvent.InterestedCount--;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} removed RSVP from event {EventId} in server {ServerId}",
            userId, request.EventId, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/events/{eventId}/rsvp", async (
            long serverId,
            long eventId,
            IRequestHandler<UnrsvpEventCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new UnrsvpEventCommand(serverId, eventId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UnrsvpEvent")
        .WithTags("Events");
    }
}
