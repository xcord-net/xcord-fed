using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that manages scheduled event lifecycle transitions.
/// Transitions events from Scheduled -> Active -> Completed based on timestamps.
/// Runs every 60 seconds.
/// </summary>
public sealed class EventLifecycleService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<EventLifecycleService> logger)
    : PollingBackgroundService(serviceScopeFactory, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(60);

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outboxWriter = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

        var now = DateTime.UtcNow;

        // Transition Scheduled -> Active (start time has passed)
        var eventsToActivate = await dbContext.ScheduledEvents
            .Where(e => e.Status == EventStatus.Scheduled &&
                        e.ScheduledStartTime <= now)
            .ToListAsync(ct);

        foreach (var scheduledEvent in eventsToActivate)
        {
            scheduledEvent.Status = EventStatus.Active;

            // Write outbox event for SignalR notification
            await outboxWriter.WriteAsync(
                dbContext,
                "Event.Started",
                new
                {
                    EventId = scheduledEvent.Id,
                    ServerId = scheduledEvent.ServerId,
                    Name = scheduledEvent.Name
                },
                ct);
        }

        if (eventsToActivate.Count > 0)
        {
            Logger.LogInformation(
                "Activated {Count} scheduled events",
                eventsToActivate.Count);
        }

        // Transition Active -> Completed (end time has passed)
        var eventsToComplete = await dbContext.ScheduledEvents
            .Where(e => e.Status == EventStatus.Active &&
                        e.ScheduledEndTime != null &&
                        e.ScheduledEndTime <= now)
            .ToListAsync(ct);

        foreach (var scheduledEvent in eventsToComplete)
        {
            scheduledEvent.Status = EventStatus.Completed;

            // Write outbox event for SignalR notification
            await outboxWriter.WriteAsync(
                dbContext,
                "Event.Completed",
                new
                {
                    EventId = scheduledEvent.Id,
                    ServerId = scheduledEvent.ServerId,
                    Name = scheduledEvent.Name
                },
                ct);
        }

        if (eventsToComplete.Count > 0)
        {
            Logger.LogInformation(
                "Completed {Count} active events",
                eventsToComplete.Count);
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
