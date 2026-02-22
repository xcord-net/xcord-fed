using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that manages scheduled event lifecycle transitions.
/// Transitions events from Scheduled -> Active -> Completed based on timestamps.
/// Runs every 60 seconds.
/// </summary>
public sealed class EventLifecycleService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<EventLifecycleService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(60);

    public EventLifecycleService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<EventLifecycleService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventLifecycleService background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEventTransitionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event lifecycle transitions");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("EventLifecycleService background service stopped");
    }

    private async Task ProcessEventTransitionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outboxWriter = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

        var now = DateTime.UtcNow;

        // Transition Scheduled -> Active (start time has passed)
        var eventsToActivate = await dbContext.ScheduledEvents
            .Where(e => e.Status == EventStatus.Scheduled &&
                        e.ScheduledStartTime <= now)
            .ToListAsync(cancellationToken);

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
                cancellationToken);
        }

        if (eventsToActivate.Count > 0)
        {
            _logger.LogInformation(
                "Activated {Count} scheduled events",
                eventsToActivate.Count);
        }

        // Transition Active -> Completed (end time has passed)
        var eventsToComplete = await dbContext.ScheduledEvents
            .Where(e => e.Status == EventStatus.Active &&
                        e.ScheduledEndTime != null &&
                        e.ScheduledEndTime <= now)
            .ToListAsync(cancellationToken);

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
                cancellationToken);
        }

        if (eventsToComplete.Count > 0)
        {
            _logger.LogInformation(
                "Completed {Count} active events",
                eventsToComplete.Count);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
