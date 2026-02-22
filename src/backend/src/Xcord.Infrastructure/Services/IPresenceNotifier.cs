using Xcord.Entities;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Interface for broadcasting presence change notifications.
/// Implemented in the API layer using SignalR hub context.
/// </summary>
public interface IPresenceNotifier
{
    /// <summary>
    /// Notifies all relevant server groups about a user's presence change.
    /// </summary>
    Task NotifyPresenceChangedAsync(long userId, PresenceStatus status, IEnumerable<long> serverIds);
}
