using System.Threading.Channels;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// In-memory queue for events that need to be delivered to outgoing webhooks.
/// NotificationService publishes events here; OutgoingWebhookEventProcessor reads them.
/// </summary>
public sealed class OutgoingWebhookEventQueue
{
    private readonly Channel<WebhookQueueEntry> _channel = Channel.CreateUnbounded<WebhookQueueEntry>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(string eventType, string payload, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(new WebhookQueueEntry(eventType, payload), ct);

    public IAsyncEnumerable<WebhookQueueEntry> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}

public readonly record struct WebhookQueueEntry(string EventType, string Payload);
