namespace Xcord.Entities;

/// <summary>
/// Represents an outgoing webhook that pushes event payloads to an external URL
/// when configured server events occur.
/// Secret is encrypted at rest using IEncryptionService (AES-256-GCM).
/// </summary>
public sealed class OutgoingWebhook : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The server this webhook belongs to.
    /// </summary>
    public long ServerId { get; set; }

    /// <summary>
    /// The external URL to deliver payloads to (max 2048 characters).
    /// </summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 signing secret, encrypted at rest (bytea).
    /// Contains 32 random bytes encrypted with IEncryptionService.
    /// </summary>
    public byte[] Secret { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// JSON array of event type strings that trigger this webhook
    /// (e.g., ["MessageCreated","MemberJoined"]).
    /// Stored as jsonb.
    /// </summary>
    public string EventTypesJson { get; set; } = "[]";

    /// <summary>
    /// Whether this webhook is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The user who created this webhook.
    /// </summary>
    public long CreatedByUserId { get; set; }

    /// <summary>
    /// Webhook creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Navigation property to the owning Server.
    /// </summary>
    public Server Server { get; set; } = null!;

    /// <summary>
    /// Navigation property to the creator User.
    /// </summary>
    public User CreatedByUser { get; set; } = null!;
}
