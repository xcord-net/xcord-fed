using Xcord;

namespace Xcord.Entities;

public sealed class OutgoingWebhookDelivery : ISoftDeletable
{
    public long Id { get; set; }
    public long OutgoingWebhookId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public int? StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTimeOffset DeliveredAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public OutgoingWebhook OutgoingWebhook { get; set; } = null!;
}
