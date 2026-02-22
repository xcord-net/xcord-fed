using Xcord;

namespace Xcord.Entities;

public sealed class OutgoingWebhook : ISoftDeletable
{
    public long Id { get; set; }
    public long ServerId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string EventTypes { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public int FailureCount { get; set; }
    public DateTimeOffset? LastFailureAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Server Server { get; set; } = null!;
    public ICollection<OutgoingWebhookDelivery> Deliveries { get; set; } = new List<OutgoingWebhookDelivery>();
}
