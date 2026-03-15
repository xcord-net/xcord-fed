using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
public sealed class OutgoingWebhookDeliveryCleanup(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<OutgoingWebhookDeliveryCleanup> logger)
    : PollingBackgroundService(serviceScopeFactory, logger)
{
    private static readonly TimeSpan DeliveredRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan DeadLetteredRetention = TimeSpan.FromDays(30);

    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;

        // Hard-delete Delivered entries older than 7 days
        var deliveredCutoff = now.Subtract(DeliveredRetention);
        var deliveredDeleted = await dbContext.OutgoingWebhookDeliveries
            .Where(d => d.Status == OutgoingWebhookDeliveryStatus.Delivered
                     && d.CreatedAt < deliveredCutoff)
            .ExecuteDeleteAsync(ct);

        // Hard-delete DeadLettered entries older than 30 days
        var deadLetteredCutoff = now.Subtract(DeadLetteredRetention);
        var deadLetteredDeleted = await dbContext.OutgoingWebhookDeliveries
            .Where(d => d.Status == OutgoingWebhookDeliveryStatus.DeadLettered
                     && d.CreatedAt < deadLetteredCutoff)
            .ExecuteDeleteAsync(ct);

        if (deliveredDeleted > 0 || deadLetteredDeleted > 0)
        {
            Logger.LogInformation(
                "Cleaned up {Delivered} delivered and {DeadLettered} dead-lettered webhook delivery records",
                deliveredDeleted, deadLetteredDeleted);
        }
    }
}
