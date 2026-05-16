namespace Xcord.Infrastructure.Services;

/// <summary>
/// Dispatches notifications (SignalR events, emails) directly without intermediate storage.
/// Replaces the transactional outbox pattern with immediate dispatch.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send a SignalR event to all clients in a conversation group.
    /// </summary>
    Task NotifyConversationAsync(long conversationId, string method, object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a SignalR event to a single user's group.
    /// </summary>
    Task NotifyUserAsync(long userId, string method, object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a SignalR event to all clients in a server group.
    /// </summary>
    Task NotifyServerAsync(long serverId, string method, object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send an email via SMTP.
    /// </summary>
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
