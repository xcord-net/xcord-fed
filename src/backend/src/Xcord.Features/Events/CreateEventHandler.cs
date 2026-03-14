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

public sealed record CreateEventCommand(
    long ServerId,
    long? ChannelId,
    string Name,
    string? Description,
    string? Location,
    string? ImageUrl,
    DateTimeOffset ScheduledStartTime,
    DateTimeOffset? ScheduledEndTime
);

public sealed class CreateEventHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<CreateEventHandler> logger)
    : IRequestHandler<CreateEventCommand, Result<EventDto>>, IValidatable<CreateEventCommand>
{
    public Error? Validate(CreateEventCommand request)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            return Error.Validation("VALIDATION_ERROR", "Event name is required");
        }

        if (request.Name.Length > 100)
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

        if (request.ScheduledStartTime <= DateTimeOffset.UtcNow)
        {
            return Error.Validation("VALIDATION_ERROR", "Scheduled start time must be in the future");
        }

        if (request.ScheduledEndTime.HasValue && request.ScheduledEndTime.Value <= request.ScheduledStartTime)
        {
            return Error.Validation("VALIDATION_ERROR", "Scheduled end time must be after scheduled start time");
        }

        return null;
    }

    public async Task<Result<EventDto>> Handle(CreateEventCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify server exists
        var server = await dbContext.Servers
            .FirstOrDefaultAsync(s => s.Id == request.ServerId, cancellationToken);

        if (server == null)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Verify user has ManageEvents permission
        var permissionResult = await roleService.EnsureServerRole(
            userId,
            request.ServerId,
            Role.ManageEvents);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // If ChannelId is provided, verify it belongs to this server
        if (request.ChannelId.HasValue)
        {
            var channel = await dbContext.Channels
                .FirstOrDefaultAsync(c => c.Id == request.ChannelId.Value && c.ServerId == request.ServerId, cancellationToken);

            if (channel == null)
            {
                return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found in this server");
            }
        }

        var now = DateTimeOffset.UtcNow;

        // Create scheduled event
        var eventId = snowflakeGenerator.NextId();
        var scheduledEvent = new ScheduledEvent
        {
            Id = eventId,
            ServerId = request.ServerId,
            CreatorId = userId,
            ChannelId = request.ChannelId,
            Name = request.Name,
            Description = request.Description,
            Location = request.Location,
            ImageUrl = request.ImageUrl,
            ScheduledStartTime = request.ScheduledStartTime,
            ScheduledEndTime = request.ScheduledEndTime,
            Status = EventStatus.Scheduled,
            InterestedCount = 0,
            NotificationSent = false,
            CreatedAt = now
        };

        dbContext.ScheduledEvents.Add(scheduledEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created event {EventName} (ID: {EventId}) in server {ServerId}",
            userId, scheduledEvent.Name, eventId, request.ServerId);

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
        return app.MapPost("/api/v1/servers/{serverId}/events", async (
            long serverId,
            CreateEventRequest request,
            IRequestHandler<CreateEventCommand, Result<EventDto>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateEventCommand(
                ServerId: serverId,
                ChannelId: request.ChannelId,
                Name: request.Name,
                Description: request.Description,
                Location: request.Location,
                ImageUrl: request.ImageUrl,
                ScheduledStartTime: request.ScheduledStartTime,
                ScheduledEndTime: request.ScheduledEndTime
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateEvent")
        .WithTags("Events");
    }
}
