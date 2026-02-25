using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using Xcord.Infrastructure.Services;

namespace Xcord.Api;

/// <summary>
/// Implementation of IEventDispatcher that dispatches events via SignalR.
/// Bot_* events are forwarded to the bot's configured interaction endpoint.
/// </summary>
public class SignalREventDispatcher : IEventDispatcher
{
    private readonly IHubContext<MainHub> _hubContext;
    private readonly ILogger<SignalREventDispatcher> _logger;
    private readonly IEmailService? _emailService;
    private readonly BotInteractionForwarder? _botInteractionForwarder;

    public SignalREventDispatcher(
        IHubContext<MainHub> hubContext,
        ILogger<SignalREventDispatcher> logger,
        IEmailService? emailService = null,
        BotInteractionForwarder? botInteractionForwarder = null)
    {
        _hubContext = hubContext;
        _logger = logger;
        _emailService = emailService;
        _botInteractionForwarder = botInteractionForwarder;
    }

    public async Task DispatchAsync(string eventType, string payload)
    {
        _logger.LogDebug("Dispatching event {EventType} with payload: {Payload}", eventType, payload);

        // Handle Email events
        if (eventType.StartsWith("Email."))
        {
            await DispatchEmailEventAsync(eventType, payload);
            return;
        }

        // Handle Bot interaction events — forward to the bot's webhook endpoint.
        // These are not dispatched via SignalR; they are HTTP POSTs to an external URL.
        if (eventType.StartsWith("Bot_"))
        {
            if (_botInteractionForwarder != null)
            {
                await _botInteractionForwarder.ForwardAsync(eventType, payload, CancellationToken.None);
            }
            else
            {
                _logger.LogWarning("Bot event {EventType} received but BotInteractionForwarder is not registered", eventType);
            }
            return;
        }

        // Parse payload to extract routing information
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        // Handle Call events (route to DM member user groups)
        if (eventType.StartsWith("Call."))
        {
            await DispatchDmEventAsync(eventType, root, root);
            return;
        }

        // Handle Moderation events (route to server group for mods)
        if (eventType.StartsWith("Member.") || eventType.StartsWith("Report.") || eventType.StartsWith("Automod."))
        {
            await DispatchServerEventAsync(eventType, root, root);
            return;
        }

        // Handle DM-specific events
        if (eventType.StartsWith("Dm."))
        {
            await DispatchDmEventAsync(eventType, root, root);
            return;
        }

        // Handle Friend-specific events
        if (eventType.StartsWith("Friend."))
        {
            await DispatchFriendEventAsync(eventType, root, root);
            return;
        }

        // Handle User-specific events (Block/Unblock)
        if (eventType.StartsWith("User."))
        {
            await DispatchUserEventAsync(eventType, root, root);
            return;
        }

        // Handle Channel events — routed to the whole server group so all members
        // see sidebar updates without needing to be in a conversation group.
        if (eventType.StartsWith("Chat."))
        {
            await DispatchServerEventAsync(eventType, root, root);
            return;
        }

        // Handle Notify events routed to specific users (e.g. unread counts)
        if (eventType.StartsWith("Notify."))
        {
            await DispatchNotifyEventAsync(eventType, root, root);
            return;
        }

        // Extract conversationId for routing
        if (!root.TryGetProperty("conversationId", out var conversationIdElement))
        {
            _logger.LogWarning("Event {EventType} payload missing conversationId, skipping dispatch", eventType);
            return;
        }

        if (!TryGetId(conversationIdElement, out var conversationId))
        {
            _logger.LogWarning("Event {EventType} conversationId is not a valid long, skipping dispatch", eventType);
            return;
        }
        var groupName = $"conversation:{conversationId}";

        // Map event type to SignalR method name
        var methodName = MapEventTypeToMethod(eventType);

        _logger.LogInformation(
            "Dispatching {EventType} as {MethodName} to group {GroupName}",
            eventType,
            methodName,
            groupName);

        // Send the parsed payload object so clients receive a JSON object, not a raw string.
        await _hubContext.Clients.Group(groupName).SendAsync(methodName, root);
    }

    private async Task DispatchEmailEventAsync(string eventType, string payload)
    {
        if (_emailService == null)
        {
            _logger.LogWarning("Email event {EventType} received but no IEmailService is registered", eventType);
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("to", out var toElement) ||
                !root.TryGetProperty("subject", out var subjectElement) ||
                !root.TryGetProperty("htmlBody", out var htmlBodyElement))
            {
                _logger.LogWarning("Email event {EventType} missing required fields (to, subject, htmlBody)", eventType);
                return;
            }

            var to = toElement.GetString();
            var subject = subjectElement.GetString();
            var htmlBody = htmlBodyElement.GetString();

            if (string.IsNullOrEmpty(to) || string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(htmlBody))
            {
                _logger.LogWarning("Email event {EventType} has empty required fields", eventType);
                return;
            }

            _logger.LogInformation("Sending email for event {EventType} to {To}", eventType, to);
            await _emailService.SendAsync(to, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch email event {EventType}", eventType);
        }
    }

    private async Task DispatchDmEventAsync(string eventType, JsonElement root, JsonElement data)
    {
        // Extract memberIds from DM event payload
        if (!root.TryGetProperty("memberIds", out var memberIdsElement))
        {
            _logger.LogWarning("DM event {EventType} payload missing memberIds, skipping dispatch", eventType);
            return;
        }

        var memberIds = memberIdsElement.EnumerateArray()
            .Select(e => TryGetId(e, out var id) ? id : (long?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();

        var methodName = MapEventTypeToMethod(eventType);

        _logger.LogInformation(
            "Dispatching {EventType} as {MethodName} to {MemberCount} user groups",
            eventType,
            methodName,
            memberIds.Length);

        // Send the parsed payload object so clients receive a JSON object, not a raw string.
        foreach (var memberId in memberIds)
        {
            var userGroup = $"user:{memberId}";
            await _hubContext.Clients.Group(userGroup).SendAsync(methodName, data);
        }
    }

    private async Task DispatchFriendEventAsync(string eventType, JsonElement root, JsonElement data)
    {
        // Extract receiverId for Friend.RequestSent or senderId for Friend.Accepted
        long targetUserId;
        if (eventType == "Friend.RequestSent")
        {
            if (!root.TryGetProperty("receiverId", out var receiverIdElement) ||
                !TryGetId(receiverIdElement, out targetUserId))
            {
                _logger.LogWarning("Friend event {EventType} payload missing or invalid receiverId, skipping dispatch", eventType);
                return;
            }
        }
        else if (eventType == "Friend.Accepted")
        {
            if (!root.TryGetProperty("senderId", out var senderIdElement) ||
                !TryGetId(senderIdElement, out targetUserId))
            {
                _logger.LogWarning("Friend event {EventType} payload missing or invalid senderId, skipping dispatch", eventType);
                return;
            }
        }
        else
        {
            _logger.LogWarning("Unknown friend event type {EventType}", eventType);
            return;
        }

        var methodName = MapEventTypeToMethod(eventType);
        var userGroup = $"user:{targetUserId}";

        _logger.LogInformation(
            "Dispatching {EventType} as {MethodName} to user group {UserGroup}",
            eventType,
            methodName,
            userGroup);

        await _hubContext.Clients.Group(userGroup).SendAsync(methodName, data);
    }

    private async Task DispatchUserEventAsync(string eventType, JsonElement root, JsonElement data)
    {
        // Extract blockedId for User.Blocked/Unblocked
        if (!root.TryGetProperty("blockedId", out var blockedIdElement) ||
            !TryGetId(blockedIdElement, out var blockedId))
        {
            _logger.LogWarning("User event {EventType} payload missing or invalid blockedId, skipping dispatch", eventType);
            return;
        }
        var methodName = MapEventTypeToMethod(eventType);
        var userGroup = $"user:{blockedId}";

        _logger.LogInformation(
            "Dispatching {EventType} as {MethodName} to user group {UserGroup}",
            eventType,
            methodName,
            userGroup);

        await _hubContext.Clients.Group(userGroup).SendAsync(methodName, data);
    }

    private async Task DispatchNotifyEventAsync(string eventType, JsonElement root, JsonElement data)
    {
        // Extract userId — Notify events target a single user's group
        if (!root.TryGetProperty("userId", out var userIdElement))
        {
            _logger.LogWarning("Notify event {EventType} payload missing userId, skipping dispatch", eventType);
            return;
        }

        if (!TryGetId(userIdElement, out var userId))
        {
            _logger.LogWarning("Notify event {EventType} userId is not a valid long, skipping dispatch", eventType);
            return;
        }

        var methodName = MapEventTypeToMethod(eventType);
        var userGroup = $"user:{userId}";

        _logger.LogInformation(
            "Dispatching {EventType} as {MethodName} to user group {UserGroup}",
            eventType,
            methodName,
            userGroup);

        await _hubContext.Clients.Group(userGroup).SendAsync(methodName, data);
    }

    private async Task DispatchServerEventAsync(string eventType, JsonElement root, JsonElement data)
    {
        if (!root.TryGetProperty("ServerId", out var serverIdElement) &&
            !root.TryGetProperty("serverId", out serverIdElement))
        {
            _logger.LogWarning("Server event {EventType} payload missing ServerId, skipping dispatch", eventType);
            return;
        }

        if (!TryGetId(serverIdElement, out var serverId))
        {
            _logger.LogWarning("Server event {EventType} ServerId is not a valid long, skipping dispatch", eventType);
            return;
        }
        var methodName = MapEventTypeToMethod(eventType);
        var groupName = $"server:{serverId}";

        _logger.LogInformation(
            "Dispatching {EventType} as {MethodName} to server group {GroupName}",
            eventType,
            methodName,
            groupName);

        await _hubContext.Clients.Group(groupName).SendAsync(methodName, data);
    }

    /// <summary>
    /// Extract a long ID from a JsonElement that may be either a JSON number or a JSON string
    /// (the latter occurs when SnowflakeJsonConverter was used to serialize the payload).
    /// </summary>
    private static bool TryGetId(JsonElement element, out long value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetInt64(out value);
        }
        if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString();
            return long.TryParse(str, out value);
        }
        value = 0;
        return false;
    }

    private static string MapEventTypeToMethod(string eventType)
    {
        // Map event types to the SignalR method names that clients register listeners for.
        return eventType switch
        {
            // Message events — names match the client's connection.on() registrations
            "Message.Created" => "Chat_MessageCreated",
            "Message.Edited" => "Chat_MessageUpdated",
            "Message.Deleted" => "Chat_MessageDeleted",
            "Message.BulkDeleted" => "Chat_BulkMessageDeleted",
            "Reaction.Added" => "Chat_ReactionUpdated",
            "Reaction.Removed" => "Chat_ReactionUpdated",
            "Message.Pinned" => "Chat_MessagePinned",
            "Message.Unpinned" => "Chat_MessageUnpinned",
            "Message.Embedded" => "Chat_MessageEmbedded",
            // Channel events
            "Chat.ChannelCreated" => "Chat_ChannelCreated",
            // Unread events
            "Notify.UnreadUpdated" => "Notify_UnreadUpdated",
            // DM events
            "Dm.Created" => "Notify_DmCreated",
            "Dm.MemberAdded" => "Notify_DmMemberAdded",
            "Dm.MemberRemoved" => "Notify_DmMemberRemoved",
            // Friend events
            "Friend.RequestSent" => "Notify_FriendRequest",
            "Friend.Accepted" => "Notify_FriendAccepted",
            // User events
            "User.Blocked" => "Notify_UserBlocked",
            "User.Unblocked" => "Notify_UserUnblocked",
            // Moderation events
            "Member.Banned" => "Notify_MemberBanned",
            "Member.Unbanned" => "Notify_MemberUnbanned",
            "Member.TimedOut" => "Notify_MemberTimedOut",
            "Member.TimeoutRemoved" => "Notify_TimeoutRemoved",
            "Report.Created" => "Notify_Report",
            "Automod.Alert" => "Notify_AutomodAlert",
            // Call events
            "Call.Initiated" => "Notify_IncomingCall",
            "Call.Answered" => "Notify_CallAnswered",
            "Call.Declined" => "Notify_CallEnded",
            "Call.Ended" => "Notify_CallEnded",
            "Call.Missed" => "Notify_CallEnded",
            _ => eventType.Replace(".", "_")
        };
    }
}
