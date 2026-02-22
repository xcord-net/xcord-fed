namespace Xcord.Infrastructure.Services;

/// <summary>
/// Broadcasts system-level events to all connected SignalR clients.
/// Implemented in Xcord.Api using IHubContext&lt;MainHub&gt; to avoid a circular project reference.
/// </summary>
public interface ISystemBroadcaster
{
    /// <summary>
    /// Broadcast a named event with the given payload to all connected clients.
    /// </summary>
    Task BroadcastAsync(string eventName, object payload, CancellationToken cancellationToken = default);
}
