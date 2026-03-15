using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that sends notifications for upcoming scheduled events.
/// Sends a notification 15 minutes before an event starts (to RSVP'd users).
/// Runs every 60 seconds.
/// </summary>
public sealed class EventNotifier : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<EventNotifier> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _notificationWindow = TimeSpan.FromMinutes(15);

    public EventNotifier(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<EventNotifier> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventNotifier background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendEventNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending event notifications");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("EventNotifier background service stopped");
    }

    private async Task SendEventNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        var notificationThreshold = now.Add(_notificationWindow);

        // Find events that are scheduled, starting within the notification window, and haven't been notified yet
        var eventsToNotify = await dbContext.ScheduledEvents
            .Where(e => e.Status == EventStatus.Scheduled &&
                        !e.NotificationSent &&
                        e.ScheduledStartTime > now &&
                        e.ScheduledStartTime <= notificationThreshold)
            .ToListAsync(cancellationToken);

        // Collect event data and RSVP user IDs before saving
        var eventNotifications = new List<(ScheduledEvent Event, List<long> RsvpUserIds)>();

        foreach (var scheduledEvent in eventsToNotify)
        {
            var rsvpUserIds = await dbContext.EventRsvps
                .Where(r => r.EventId == scheduledEvent.Id)
                .Select(r => r.UserId)
                .ToListAsync(cancellationToken);

            eventNotifications.Add((scheduledEvent, rsvpUserIds));
            scheduledEvent.NotificationSent = true;
        }

        if (eventsToNotify.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            // Send notifications after save
            foreach (var (scheduledEvent, rsvpUserIds) in eventNotifications)
            {
                var payload = new
                {
                    EventId = scheduledEvent.Id,
                    ServerId = scheduledEvent.ServerId,
                    Name = scheduledEvent.Name,
                    ScheduledStartTime = scheduledEvent.ScheduledStartTime,
                    RsvpUserIds = rsvpUserIds
                };

                foreach (var userId in rsvpUserIds)
                {
                    await notificationService.NotifyUserAsync(userId, "Notify_EventStarting", payload);
                }
            }

            _logger.LogInformation(
                "Sent notifications for {Count} upcoming events",
                eventsToNotify.Count);
        }
    }
}
