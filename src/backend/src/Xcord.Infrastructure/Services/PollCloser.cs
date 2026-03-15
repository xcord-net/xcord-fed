using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that automatically closes expired polls.
/// Runs every 60 seconds.
/// </summary>
public sealed class PollCloser(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PollCloser> logger)
    : PollingBackgroundService(serviceScopeFactory, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(60);

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outboxWriter = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

        var now = DateTime.UtcNow;

        // Query polls that are not closed, have an expiration date, and are past expiry
        var expiredPolls = await dbContext.Polls
            .Include(p => p.Message)
            .Where(p => !p.IsClosed && p.ExpiresAt != null && p.ExpiresAt < now)
            .ToListAsync(ct);

        if (!expiredPolls.Any())
        {
            return;
        }

        Logger.LogInformation("Closing {Count} expired polls", expiredPolls.Count);

        using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

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
                }, ct);

                Logger.LogInformation("Closed expired poll {PollId}", poll.Id);
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
