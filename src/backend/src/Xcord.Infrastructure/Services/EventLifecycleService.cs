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
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;

        // Transition Scheduled -> Active (start time has passed)
        var eventsToActivate = await dbContext.ScheduledEvents
            .Where(e => e.Status == EventStatus.Scheduled &&
                        e.ScheduledStartTime <= now)
            .ToListAsync(ct);

        foreach (var scheduledEvent in eventsToActivate)
        {
            scheduledEvent.Status = EventStatus.Active;
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
        }

        if (eventsToComplete.Count > 0)
        {
            Logger.LogInformation(
                "Completed {Count} active events",
                eventsToComplete.Count);
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        // Send notifications after save
        foreach (var scheduledEvent in eventsToActivate)
        {
            await notificationService.NotifyServerAsync(
                scheduledEvent.ServerId,
                "Event_Started",
                new
                {
                    EventId = scheduledEvent.Id,
                    ServerId = scheduledEvent.ServerId,
                    Name = scheduledEvent.Name
                }, ct);
        }

        foreach (var scheduledEvent in eventsToComplete)
        {
            await notificationService.NotifyServerAsync(
                scheduledEvent.ServerId,
                "Event_Completed",
                new
                {
                    EventId = scheduledEvent.Id,
                    ServerId = scheduledEvent.ServerId,
                    Name = scheduledEvent.Name
                }, ct);
        }
    }
}
