using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that automatically marks ringing calls as missed after 30 seconds.
/// Runs every 5 seconds.
/// </summary>
public sealed class CallTimeoutService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<CallTimeoutService> _logger;

    public CallTimeoutService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<CallTimeoutService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CallTimeoutService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTimedOutCallsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing timed-out calls");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("CallTimeoutService stopped");
    }

    private async Task ProcessTimedOutCallsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
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
            .ToListAsync(cancellationToken);

        if (timedOutCalls.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} timed-out calls to process", timedOutCalls.Count);

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

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
                }, cancellationToken);

                _logger.LogInformation("Call {CallId} marked as missed (timeout)", call.Id);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
