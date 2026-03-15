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
        var outboxWriter = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

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

        using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            foreach (var call in timedOutCalls)
            {
                // Update call status to Missed
                call.Status = CallStatus.Missed;
                call.EndedAt = now;

                // Get the recipient ID
                var recipientId = call.DmChannel.Members.FirstOrDefault(m => m.UserId != call.CallerId)?.UserId;

                // Write outbox event to notify both users
                await outboxWriter.WriteAsync(dbContext, "Call.Ended", new
                {
                    CallId = call.Id,
                    DmChannelId = call.DmChannelId,
                    CallerId = call.CallerId,
                    RecipientId = recipientId,
                    Status = CallStatus.Missed
                }, ct);

                Logger.LogInformation("Call {CallId} marked as missed (timeout)", call.Id);
            }

            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
