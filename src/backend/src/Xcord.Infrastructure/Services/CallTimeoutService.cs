using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that automatically marks ringing calls as missed after 30 seconds.
/// Runs every 5 seconds.
/// </summary>
public sealed class CallTimeoutService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<CallTimeoutService> logger)
    : PollingBackgroundService(serviceScopeFactory, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(5);

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        var timeoutThreshold = now.AddSeconds(-30);

        // Find all calls that are ringing and started more than 30 seconds ago
        var timedOutCalls = await dbContext.Calls
            .Include(c => c.DmChannel)
                .ThenInclude(dm => dm.Members)
            .Where(c => c.Status == CallStatus.Ringing)
            .Where(c => c.StartedAt < timeoutThreshold)
            .ToListAsync(ct);

        if (timedOutCalls.Count == 0)
        {
            return;
        }

        Logger.LogInformation("Found {Count} timed-out calls to process", timedOutCalls.Count);

        // Collect notification data before save
        var callNotifications = new List<(long CallId, long DmChannelId, long CallerId, long? RecipientId)>();

        using var transaction = await dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            foreach (var call in timedOutCalls)
            {
                // Update call status to Missed
                call.Status = CallStatus.Missed;
                call.EndedAt = now;

                // Collect the recipient ID for notification after save
                var recipientId = call.DmChannel.Members.FirstOrDefault(m => m.UserId != call.CallerId)?.UserId;
                callNotifications.Add((call.Id, call.DmChannelId, call.CallerId, recipientId));

                Logger.LogInformation("Call {CallId} marked as missed (timeout)", call.Id);
            }

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }

        // Send notifications after successful commit
        foreach (var (callId, dmChannelId, callerId, recipientId) in callNotifications)
        {
            var payload = new
            {
                CallId = callId,
                DmChannelId = dmChannelId,
                CallerId = callerId,
                RecipientId = recipientId,
                Status = CallStatus.Missed
            };

            await notificationService.NotifyUserAsync(callerId, "Notify_CallEnded", payload, ct).ConfigureAwait(false);

            if (recipientId.HasValue)
            {
                await notificationService.NotifyUserAsync(recipientId.Value, "Notify_CallEnded", payload, ct).ConfigureAwait(false);
            }
        }
    }
}
