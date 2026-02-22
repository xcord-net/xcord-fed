namespace Xcord.Infrastructure.Services;

/// <summary>
/// Dispatches outbox events to SignalR clients.
/// </summary>
public interface IEventDispatcher
{
    /// <summary>
    /// Dispatches an event to the appropriate SignalR groups based on event type and payload.
    /// </summary>
    /// <param name="eventType">The type of event (e.g., "Message.Created", "Message.Edited").</param>
    /// <param name="payload">The JSON payload of the event.</param>
    Task DispatchAsync(string eventType, string payload);
}
