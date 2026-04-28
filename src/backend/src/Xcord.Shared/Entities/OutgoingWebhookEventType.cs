namespace Xcord.Entities;

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
}
