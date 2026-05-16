using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Events;

public sealed record DeleteEventCommand(
    long ServerId,
    long EventId
);

public sealed class DeleteEventHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<DeleteEventHandler> logger)
    : IRequestHandler<DeleteEventCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteEventCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify user has ManageEvents permission
        var permissionResult = await roleService.EnsureServerRole(
            userId,
            request.ServerId,
            Role.ManageEvents);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Get the event
        var scheduledEvent = await dbContext.ScheduledEvents
            .FirstOrDefaultAsync(e => e.Id == request.EventId && e.ServerId == request.ServerId, cancellationToken);

        if (scheduledEvent == null)
        {
            return Error.NotFound("EVENT_NOT_FOUND", "Event not found");
        }

        // Soft delete
        scheduledEvent.SoftDelete();

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} deleted event {EventId} in server {ServerId}",
            userId, request.EventId, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/events/{eventId}", async (
            long serverId,
            long eventId,
            IRequestHandler<DeleteEventCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new DeleteEventCommand(serverId, eventId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteEvent")
        .WithTags("Events");
    }
}
