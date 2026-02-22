using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that automatically closes expired polls.
/// Runs every 60 seconds.
/// </summary>
public sealed class PollCloser : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PollCloser> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(60);

    public PollCloser(
        IServiceScopeFactory scopeFactory,
        ILogger<PollCloser> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PollCloser background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CloseExpiredPollsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing expired polls");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("PollCloser background service stopped");
    }

    private async Task CloseExpiredPollsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outboxWriter = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

        var now = DateTime.UtcNow;

        // Query polls that are not closed, have an expiration date, and are past expiry
        var expiredPolls = await dbContext.Polls
            .Include(p => p.Message)
            .Where(p => !p.IsClosed && p.ExpiresAt != null && p.ExpiresAt < now)
            .ToListAsync(cancellationToken);

        if (!expiredPolls.Any())
        {
            return;
        }

        _logger.LogInformation("Closing {Count} expired polls", expiredPolls.Count);

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var poll in expiredPolls)
            {
                poll.IsClosed = true;

                // Write outbox event
                await outboxWriter.WriteAsync(dbContext, "Poll.Ended", new
                {
                    PollId = poll.Id,
                    ConversationId = poll.Message.ConversationId
                }, cancellationToken);

                _logger.LogInformation("Closed expired poll {PollId}", poll.Id);
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
