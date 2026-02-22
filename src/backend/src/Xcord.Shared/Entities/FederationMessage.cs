namespace Xcord.Entities;

/// <summary>
/// Tracks a message received via federation, linking the remote message to the local copy.
/// </summary>
public sealed class FederationMessage
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Federation follow that sourced this message (FK to FederationFollow).
    /// </summary>
    public long FederationFollowId { get; set; }

    /// <summary>
    /// Message ID from the remote instance (string).
    /// </summary>
    public string RemoteMessageId { get; set; } = string.Empty;

    /// <summary>
    /// Local message ID — the crossposted copy (FK to Message).
    /// </summary>
    public long LocalMessageId { get; set; }

    /// <summary>
    /// Display name of the remote author.
    /// </summary>
    public string RemoteAuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Avatar URL of the remote author.
    /// </summary>
    public string? RemoteAuthorAvatarUrl { get; set; }

    /// <summary>
    /// Timestamp when this message was received.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; set; }

    // Navigation properties
    public FederationFollow Follow { get; set; } = null!;
    public Message LocalMessage { get; set; } = null!;
}
