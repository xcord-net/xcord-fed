namespace Xcord.Entities;

/// <summary>
/// Represents a single delivery attempt record for an outgoing webhook.
/// NOT soft-deleted — hard-deleted by the cleanup service after retention period.
/// </summary>
public sealed class OutgoingWebhookDelivery
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The outgoing webhook this delivery belongs to.
    /// </summary>
    public long WebhookId { get; set; }

    /// <summary>
    /// The event type that triggered this delivery (e.g., "MessageCreated").
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// The full JSON payload to deliver. Stored as jsonb.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Number of delivery attempts made so far.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Timestamp of the most recent delivery attempt.
    /// </summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>
    /// Timestamp when the next delivery attempt should be made.
    /// Null means eligible immediately.
    /// </summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>
    /// Current delivery status.
    /// </summary>
    public OutgoingWebhookDeliveryStatus Status { get; set; } = OutgoingWebhookDeliveryStatus.Pending;

    /// <summary>
    /// HTTP status code returned by the last attempt (null if no attempt yet).
    /// </summary>
    public int? LastHttpStatus { get; set; }

    /// <summary>
    /// Error message from the last attempt (max 1000 characters).
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Delivery record creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Navigation property to the owning OutgoingWebhook.
    /// </summary>
    public OutgoingWebhook Webhook { get; set; } = null!;
}

/// <summary>
/// Status values for an outgoing webhook delivery.
/// </summary>
public enum OutgoingWebhookDeliveryStatus
{
    /// <summary>Delivery is pending or being retried.</summary>
    Pending = 0,
    /// <summary>Delivery was accepted with a 2xx response.</summary>
    Delivered = 1,
    /// <summary>Delivery failed but retries remain.</summary>
    Failed = 2,
    /// <summary>Delivery exhausted all retries and is permanently failed.</summary>
    DeadLettered = 3
}
