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
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

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

        using var transaction = await dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            foreach (var poll in expiredPolls)
            {
                poll.IsClosed = true;
                Logger.LogInformation("Closed expired poll {PollId}", poll.Id);
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
        foreach (var poll in expiredPolls)
        {
            await notificationService.NotifyConversationAsync(
                poll.Message.ConversationId,
                "Poll_Ended",
                new
                {
                    PollId = poll.Id,
                    ConversationId = poll.Message.ConversationId
                }, ct);
        }
    }
}
