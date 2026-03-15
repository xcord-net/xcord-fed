using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Xcord.Infrastructure.Services;

namespace Xcord.Api;

/// <summary>
/// Dispatches SignalR events and emails directly without intermediate storage.
/// Serializes payloads with Snowflake ID and enum string converters to match the API's JSON format.
/// Also enqueues events for outgoing webhook delivery.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IHubContext<MainHub> _hubContext;
    private readonly IEmailService _emailService;
    private readonly OutgoingWebhookEventQueue _webhookQueue;
    private readonly ILogger<NotificationService> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters =
        {
            new SnowflakeJsonConverter(),
            new JsonStringEnumConverter(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public NotificationService(
        IHubContext<MainHub> hubContext,
        IEmailService emailService,
        OutgoingWebhookEventQueue webhookQueue,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _emailService = emailService;
        _webhookQueue = webhookQueue;
        _logger = logger;
    }

    public async Task NotifyConversationAsync(long conversationId, string method, object payload)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var data = JsonDocument.Parse(json).RootElement.Clone();

        _logger.LogDebug("Dispatching {Method} to conversation:{ConversationId}", method, conversationId);
        await _hubContext.Clients.Group($"conversation:{conversationId}").SendAsync(method, data);
        await EnqueueWebhookIfSupportedAsync(method, json);
    }

    public async Task NotifyUserAsync(long userId, string method, object payload)
    {
        var data = JsonSerializer.SerializeToElement(payload, SerializerOptions);

        _logger.LogDebug("Dispatching {Method} to user:{UserId}", method, userId);
        await _hubContext.Clients.Group($"user:{userId}").SendAsync(method, data);
    }

    public async Task NotifyServerAsync(long serverId, string method, object payload)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var data = JsonDocument.Parse(json).RootElement.Clone();

        _logger.LogDebug("Dispatching {Method} to server:{ServerId}", method, serverId);
        await _hubContext.Clients.Group($"server:{serverId}").SendAsync(method, data);
        await EnqueueWebhookIfSupportedAsync(method, json);
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        _logger.LogDebug("Sending email to {To}", to);
        await _emailService.SendAsync(to, subject, htmlBody);
    }

    private async Task EnqueueWebhookIfSupportedAsync(string method, string json)
    {
        // Only enqueue event types that outgoing webhooks support
        var eventType = method switch
        {
            "Chat_MessageCreated" => "message.created",
            "Member_Joined" => "member.joined",
            "Member_Left" => "member.left",
            "Notify_MemberBanned" => "member.banned",
            _ => null
        };

        if (eventType != null)
        {
            await _webhookQueue.EnqueueAsync(eventType, json);
        }
    }
}
