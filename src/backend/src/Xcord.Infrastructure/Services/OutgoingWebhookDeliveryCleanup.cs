using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that periodically hard-deletes old OutgoingWebhookDelivery records.
/// Runs every hour.
/// - Delivered entries are deleted after 7 days.
/// - DeadLettered entries are deleted after 30 days.
/// </summary>
public sealed class OutgoingWebhookDeliveryCleanup : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OutgoingWebhookDeliveryCleanup> _logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan DeliveredRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan DeadLetteredRetention = TimeSpan.FromDays(30);

    public OutgoingWebhookDeliveryCleanup(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<OutgoingWebhookDeliveryCleanup> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutgoingWebhookDeliveryCleanup started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up outgoing webhook deliveries");
            }

            await Task.Delay(CleanupInterval, stoppingToken);
        }

        _logger.LogInformation("OutgoingWebhookDeliveryCleanup stopped");
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;

        // Hard-delete Delivered entries older than 7 days
        var deliveredCutoff = now.Subtract(DeliveredRetention);
        var deliveredDeleted = await dbContext.OutgoingWebhookDeliveries
            .Where(d => d.Status == OutgoingWebhookDeliveryStatus.Delivered
                     && d.CreatedAt < deliveredCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        // Hard-delete DeadLettered entries older than 30 days
        var deadLetteredCutoff = now.Subtract(DeadLetteredRetention);
        var deadLetteredDeleted = await dbContext.OutgoingWebhookDeliveries
            .Where(d => d.Status == OutgoingWebhookDeliveryStatus.DeadLettered
                     && d.CreatedAt < deadLetteredCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deliveredDeleted > 0 || deadLetteredDeleted > 0)
        {
            _logger.LogInformation(
                "Cleaned up {Delivered} delivered and {DeadLettered} dead-lettered webhook delivery records",
                deliveredDeleted, deadLetteredDeleted);
        }
    }
}
