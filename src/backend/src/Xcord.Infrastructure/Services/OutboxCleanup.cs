using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that periodically hard-deletes processed outbox events after retention period.
/// </summary>
public sealed class OutboxCleanup : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OutboxCleanup> _logger;
    private readonly OutboxOptions _options;

    public OutboxCleanup(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<OutboxCleanup> logger,
        IOptions<OutboxOptions> options)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OutboxCleanup starting with cleanup interval {CleanupIntervalMinutes} minutes and retention {RetentionMinutes} minutes",
            _options.CleanupIntervalMinutes, _options.RetentionMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up outbox events");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.CleanupIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("OutboxCleanup stopping");
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoffTime = DateTime.UtcNow.AddMinutes(-_options.RetentionMinutes);

        // Hard-delete processed events older than retention period
        var deletedCount = await context.OutboxEvents
            .Where(e => e.ProcessedAt != null && e.ProcessedAt < cutoffTime)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Cleaned up {DeletedCount} processed outbox events older than {CutoffTime}", deletedCount, cutoffTime);
        }
    }
}
