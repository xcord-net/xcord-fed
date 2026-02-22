using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that automatically archives inactive threads
/// based on their AutoArchiveDurationMinutes setting.
/// Runs every 5 minutes.
/// </summary>
public sealed class ThreadArchiver : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ThreadArchiver> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public ThreadArchiver(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ThreadArchiver> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ThreadArchiver background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ArchiveInactiveThreadsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving inactive threads");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("ThreadArchiver background service stopped");
    }

    private async Task ArchiveInactiveThreadsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // Find threads that should be auto-archived
        var threadsToArchive = await dbContext.Threads
            .Where(t => !t.IsArchived &&
                        t.LastActivityAt.AddMinutes(t.AutoArchiveDurationMinutes) < now)
            .ToListAsync(cancellationToken);

        if (threadsToArchive.Count == 0)
        {
            return;
        }

        foreach (var thread in threadsToArchive)
        {
            thread.IsArchived = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Auto-archived {Count} inactive threads",
            threadsToArchive.Count);
    }
}
