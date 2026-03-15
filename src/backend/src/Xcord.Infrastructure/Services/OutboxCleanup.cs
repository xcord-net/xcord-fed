using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that periodically hard-deletes processed outbox events after retention period.
/// </summary>
public sealed class OutboxCleanup(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<OutboxCleanup> logger,
    IOptions<OutboxOptions> options)
    : PollingBackgroundService(serviceScopeFactory, logger)
{
    private readonly OutboxOptions _options = options.Value;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(_options.CleanupIntervalMinutes);

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoffTime = DateTime.UtcNow.AddMinutes(-_options.RetentionMinutes);

        // Hard-delete processed events older than retention period
        var deletedCount = await context.OutboxEvents
            .Where(e => e.ProcessedAt != null && e.ProcessedAt < cutoffTime)
            .ExecuteDeleteAsync(ct);

        if (deletedCount > 0)
        {
            Logger.LogInformation("Cleaned up {DeletedCount} processed outbox events older than {CutoffTime}", deletedCount, cutoffTime);
        }
    }
}
