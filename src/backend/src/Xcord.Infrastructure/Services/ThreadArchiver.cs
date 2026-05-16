using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that automatically archives inactive threads
/// based on their AutoArchiveDurationMinutes setting.
/// Runs every 5 minutes.
/// </summary>
public sealed class ThreadArchiver(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ThreadArchiver> logger)
    : PollingBackgroundService(serviceScopeFactory, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // Find threads that should be auto-archived
        var threadsToArchive = await dbContext.Threads
            .Where(t => !t.IsArchived &&
                        t.LastActivityAt.AddMinutes(t.AutoArchiveDurationMinutes) < now)
            .ToListAsync(ct);

        if (threadsToArchive.Count == 0)
        {
            return;
        }

        foreach (var thread in threadsToArchive)
        {
            thread.IsArchived = true;
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        Logger.LogInformation(
            "Auto-archived {Count} inactive threads",
            threadsToArchive.Count);
    }
}
