using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Events;

public sealed record UpdateEventCommand(
    long ServerId,
    long EventId,
    string? Name,
    string? Description,
    string? Location,
    string? ImageUrl,
    DateTimeOffset? ScheduledStartTime,
    DateTimeOffset? ScheduledEndTime,
    EventStatus? Status
);

public sealed class UpdateEventHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    ILogger<UpdateEventHandler> logger)
    : IRequestHandler<UpdateEventCommand, Result<EventDto>>, IValidatable<UpdateEventCommand>
{
    public Error? Validate(UpdateEventCommand request)
    {
        if (request.Name != null && request.Name.Length > 100)
        {
            return Error.Validation("VALIDATION_ERROR", "Event name must not exceed 100 characters");
        }

        if (request.Description != null && request.Description.Length > 1000)
        {
            return Error.Validation("VALIDATION_ERROR", "Event description must not exceed 1000 characters");
        }

        if (request.Location != null && request.Location.Length > 100)
        {
            return Error.Validation("VALIDATION_ERROR", "Event location must not exceed 100 characters");
        }

        if (request.ImageUrl != null && request.ImageUrl.Length > 512)
        {
            return Error.Validation("VALIDATION_ERROR", "Event image URL must not exceed 512 characters");
        }

        if (request.ScheduledEndTime.HasValue && request.ScheduledStartTime.HasValue && request.ScheduledEndTime.Value <= request.ScheduledStartTime.Value)
        {
            return Error.Validation("VALIDATION_ERROR", "Scheduled end time must be after scheduled start time");
        }

        return null;
    }

    public async Task<Result<EventDto>> Handle(UpdateEventCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify user has ManageEvents permission
        var permissionResult = await permissionService.EnsureServerPermission(
            userId,
            request.ServerId,
            Permission.ManageEvents);

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

        // Update fields if provided
        if (request.Name != null)
        {
            scheduledEvent.Name = request.Name;
        }

        if (request.Description != null)
        {
            scheduledEvent.Description = request.Description;
        }

        if (request.Location != null)
        {
            scheduledEvent.Location = request.Location;
        }

        if (request.ImageUrl != null)
        {
            scheduledEvent.ImageUrl = request.ImageUrl;
        }

        if (request.ScheduledStartTime.HasValue)
        {
            scheduledEvent.ScheduledStartTime = request.ScheduledStartTime.Value;
        }

        if (request.ScheduledEndTime.HasValue)
        {
            scheduledEvent.ScheduledEndTime = request.ScheduledEndTime.Value;
        }

        if (request.Status.HasValue)
        {
            scheduledEvent.Status = request.Status.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} updated event {EventId} in server {ServerId}",
            userId, request.EventId, request.ServerId);

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
        return app.MapPatch("/api/v1/servers/{serverId}/events/{eventId}", async (
            long serverId,
            long eventId,
            UpdateEventRequest request,
            IRequestHandler<UpdateEventCommand, Result<EventDto>> handler,
            CancellationToken ct) =>
        {
            var command = new UpdateEventCommand(
                ServerId: serverId,
                EventId: eventId,
                Name: request.Name,
                Description: request.Description,
                Location: request.Location,
                ImageUrl: request.ImageUrl,
                ScheduledStartTime: request.ScheduledStartTime,
                ScheduledEndTime: request.ScheduledEndTime,
                Status: request.Status
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateEvent")
        .WithTags("Events");
    }
}
