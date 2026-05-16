using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that reads events from the in-memory webhook queue
/// and creates OutgoingWebhookDelivery records for servers with active webhooks.
/// </summary>
public sealed class OutgoingWebhookEventProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly OutgoingWebhookEventQueue _queue;
    private readonly ILogger<OutgoingWebhookEventProcessor> _logger;

    public OutgoingWebhookEventProcessor(
        IServiceScopeFactory serviceScopeFactory,
        OutgoingWebhookEventQueue queue,
        ILogger<OutgoingWebhookEventProcessor> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutgoingWebhookEventProcessor started");

        await foreach (var entry in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessEventAsync(entry, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outgoing webhook event {EventType}", entry.EventType);
            }
        }

        _logger.LogInformation("OutgoingWebhookEventProcessor stopped");
    }

    private async Task ProcessEventAsync(WebhookQueueEntry entry, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snowflakeGenerator = scope.ServiceProvider.GetRequiredService<SnowflakeIdGenerator>();

        var serverId = ExtractServerId(entry.Payload);
        if (serverId == null)
            return;

        var activeWebhooks = await dbContext.OutgoingWebhooks
            .AsNoTracking()
            .Where(w => w.IsActive && w.ServerId == serverId.Value)
            .ToListAsync(cancellationToken);

        if (activeWebhooks.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        int deliveriesCreated = 0;

        foreach (var webhook in activeWebhooks)
        {
            string[] subscribedTypes;
            try
            {
                subscribedTypes = System.Text.Json.JsonSerializer.Deserialize<string[]>(webhook.EventTypesJson) ?? [];
            }
            catch
            {
                continue;
            }

            if (!subscribedTypes.Contains(entry.EventType))
                continue;

            var deliveryPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                eventType = entry.EventType,
                data = System.Text.Json.JsonDocument.Parse(entry.Payload).RootElement
            });

            var delivery = new OutgoingWebhookDelivery
            {
                Id = snowflakeGenerator.NextId(),
                WebhookId = webhook.Id,
                EventType = entry.EventType,
                Payload = deliveryPayload,
                AttemptCount = 0,
                Status = OutgoingWebhookDeliveryStatus.Pending,
                NextAttemptAt = null,
                CreatedAt = now
            };

            dbContext.OutgoingWebhookDeliveries.Add(delivery);
            deliveriesCreated++;
        }

        if (deliveriesCreated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Created {Count} outgoing webhook deliveries for event {EventType}", deliveriesCreated, entry.EventType);
        }
    }

    private static long? ExtractServerId(string payload)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            var root = doc.RootElement;

            foreach (var key in new[] { "serverId", "ServerId" })
            {
                if (root.TryGetProperty(key, out var element))
                {
                    if (element.ValueKind == System.Text.Json.JsonValueKind.Number &&
                        element.TryGetInt64(out var id))
                        return id;

                    if (element.ValueKind == System.Text.Json.JsonValueKind.String &&
                        long.TryParse(element.GetString(), out var strId))
                        return strId;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
