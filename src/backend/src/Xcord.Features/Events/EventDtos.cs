using Xcord.Entities;

namespace Xcord.Features.Events;

public sealed record EventDto(
    long Id,
    long ServerId,
    long CreatorId,
    long? ChannelId,
    string Name,
    string? Description,
    string? Location,
    string? ImageUrl,
    DateTimeOffset ScheduledStartTime,
    DateTimeOffset? ScheduledEndTime,
    EventStatus Status,
    int InterestedCount,
    DateTimeOffset CreatedAt
);

public sealed record CreateEventRequest(
    long? ChannelId,
    string Name,
    string? Description,
    string? Location,
    string? ImageUrl,
    DateTimeOffset ScheduledStartTime,
    DateTimeOffset? ScheduledEndTime
);

public sealed record UpdateEventRequest(
    string? Name,
    string? Description,
    string? Location,
    string? ImageUrl,
    DateTimeOffset? ScheduledStartTime,
    DateTimeOffset? ScheduledEndTime,
    EventStatus? Status
);
