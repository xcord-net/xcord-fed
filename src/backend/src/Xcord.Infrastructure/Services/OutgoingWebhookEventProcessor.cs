using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that monitors the outbox for events that have active
/// outgoing webhooks and creates OutgoingWebhookDelivery records for them.
/// Polls the outbox events table every 5 seconds, looking for events
/// where ProcessedAt is set (i.e. the SignalR dispatch already occurred)
/// and that correspond to supported outgoing webhook event types.
///
/// Uses a high-water mark (last processed outbox event ID) stored in memory
/// to avoid re-scanning already handled events.
/// </summary>
public sealed class OutgoingWebhookEventProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OutgoingWebhookEventProcessor> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 100;

    // High-water mark: the highest outbox event ID we have already processed.
    // Starts at 0 so the first run picks up everything from the beginning.
    private long _lastProcessedId;

    public OutgoingWebhookEventProcessor(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<OutgoingWebhookEventProcessor> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutgoingWebhookEventProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outgoing webhook events");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("OutgoingWebhookEventProcessor stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snowflakeGenerator = scope.ServiceProvider.GetRequiredService<SnowflakeIdGenerator>();

        // Find processed outbox events above the high-water mark whose event type
        // maps to a supported outgoing webhook event type.
        var supportedOutboxTypes = new[]
        {
            "Message.Created",
            "Member.Joined",
            "Member.Left",
            "Member.Banned"
        };

        var currentWatermark = _lastProcessedId;

        // Use IgnoreQueryFilters to include events from the raw table without soft-delete filters
        var events = await dbContext.OutboxEvents
            .Where(e => e.Id > currentWatermark
                     && e.ProcessedAt != null
                     && supportedOutboxTypes.Contains(e.EventType))
            .OrderBy(e => e.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
            return;

        _logger.LogDebug("OutgoingWebhookEventProcessor found {Count} events to process", events.Count);

        // Load all active outgoing webhooks - we'll filter per-event in memory.
        // This is efficient because the number of webhooks is bounded (<=10 per server, <=N servers).
        var activeWebhooks = await dbContext.OutgoingWebhooks
            .AsNoTracking()
            .Where(w => w.IsActive)
            .ToListAsync(cancellationToken);

        if (activeWebhooks.Count == 0)
        {
            // No active webhooks - still advance the watermark
            _lastProcessedId = events[^1].Id;
            return;
        }

        // Group webhooks by ServerId for quick lookup
        var webhooksByServer = activeWebhooks
            .GroupBy(w => w.ServerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var now = DateTimeOffset.UtcNow;
        int deliveriesCreated = 0;

        foreach (var evt in events)
        {
            var webhookEventType = OutgoingWebhookEventType.FromOutboxEventType(evt.EventType);
            if (webhookEventType == null)
                continue;

            // Extract serverId from the event payload
            var serverId = ExtractServerId(evt.EventType, evt.Payload);
            if (serverId == null)
                continue;

            if (!webhooksByServer.TryGetValue(serverId.Value, out var serverWebhooks))
                continue;

            // Build the delivery payload (re-use the outbox event payload as-is)
            // Wrap it so the receiver gets event type + data
            foreach (var webhook in serverWebhooks)
            {
                // Check that this webhook subscribes to this event type
                string[] subscribedTypes;
                try
                {
                    subscribedTypes = System.Text.Json.JsonSerializer.Deserialize<string[]>(webhook.EventTypesJson) ?? [];
                }
                catch
                {
                    continue;
                }

                if (!subscribedTypes.Contains(webhookEventType))
                    continue;

                var deliveryPayload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    eventType = webhookEventType,
                    data = System.Text.Json.JsonDocument.Parse(evt.Payload).RootElement
                });

                var delivery = new OutgoingWebhookDelivery
                {
                    Id = snowflakeGenerator.NextId(),
                    WebhookId = webhook.Id,
                    EventType = webhookEventType,
                    Payload = deliveryPayload,
                    AttemptCount = 0,
                    Status = OutgoingWebhookDeliveryStatus.Pending,
                    NextAttemptAt = null, // eligible immediately
                    CreatedAt = now
                };

                dbContext.OutgoingWebhookDeliveries.Add(delivery);
                deliveriesCreated++;
            }
        }

        if (deliveriesCreated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "OutgoingWebhookEventProcessor created {Count} delivery records",
                deliveriesCreated);
        }

        // Advance watermark to the highest ID we processed
        _lastProcessedId = events[^1].Id;
    }

    /// <summary>
    /// Extracts the serverId from an outbox event's JSON payload.
    /// Different event types embed serverId under different key names.
    /// </summary>
    private static long? ExtractServerId(string eventType, string payload)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            var root = doc.RootElement;

            // Try common property names (camelCase and PascalCase)
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

            // For Message.Created events, we need to look up the server via conversationId
            // This is a lightweight lookup - we only do it if needed
            if (eventType == "Message.Created")
            {
                // Message.Created payload has conversationId, not serverId directly.
                // We cannot do DB lookups here (no scope). Return null and rely on
                // the OutgoingWebhookDeliveryService to handle this case.
                // For now, Message.Created is only supported via direct serverId payloads.
                // If the handler that creates the outbox event wants webhook support,
                // it should include serverId in the payload.
                return null;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
