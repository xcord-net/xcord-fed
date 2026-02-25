namespace Xcord.Entities;

/// <summary>
/// Defines the supported event type strings for outgoing webhooks.
/// These map to outbox event types: "Message.Created" -> "MessageCreated", etc.
/// </summary>
public static class OutgoingWebhookEventType
{
    public const string MessageCreated = "MessageCreated";
    public const string MemberJoined = "MemberJoined";
    public const string MemberLeft = "MemberLeft";
    public const string MemberBanned = "MemberBanned";

    public static readonly string[] All =
    [
        MessageCreated,
        MemberJoined,
        MemberLeft,
        MemberBanned
    ];

    /// <summary>
    /// Maps an outbox event type string (e.g., "Message.Created") to the
    /// corresponding outgoing webhook event type (e.g., "MessageCreated").
    /// Returns null if the event type does not have a webhook mapping.
    /// </summary>
    public static string? FromOutboxEventType(string outboxEventType) => outboxEventType switch
    {
        "Message.Created" => MessageCreated,
        "Member.Joined"   => MemberJoined,
        "Member.Left"     => MemberLeft,
        "Member.Banned"   => MemberBanned,
        _                 => null
    };
}
