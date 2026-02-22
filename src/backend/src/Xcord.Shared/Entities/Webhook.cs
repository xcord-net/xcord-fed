namespace Xcord.Entities;

/// <summary>
/// Represents an incoming webhook for posting messages to a channel.
/// Token is stored in plain text (used in URL for webhook requests).
/// </summary>
public sealed class Webhook : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Secret token used in webhook URL (NOT hashed - needed for URL matching).
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Channel this webhook posts to.
    /// </summary>
    public long ChannelId { get; set; }

    /// <summary>
    /// Display name for the webhook (max 80 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Avatar URL for webhook messages (max 512 characters).
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// User who created this webhook (nullable - SetNull on user deletion).
    /// </summary>
    public long? CreatedByUserId { get; set; }

    /// <summary>
    /// Navigation property to the creator User.
    /// </summary>
    public User? CreatedByUser { get; set; }

    /// <summary>
    /// Webhook creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
