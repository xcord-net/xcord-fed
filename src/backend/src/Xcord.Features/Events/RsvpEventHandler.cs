using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Events;

public sealed record RsvpEventCommand(
    long ServerId,
    long EventId
);

public sealed class RsvpEventHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<RsvpEventHandler> logger)
    : IRequestHandler<RsvpEventCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RsvpEventCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

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

        await dbContext.SaveChangesAsync(cancellationToken);

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
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("RsvpEvent")
        .WithTags("Events");
    }
}
