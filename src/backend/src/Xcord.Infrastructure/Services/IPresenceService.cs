using Xcord.Entities;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for managing user presence state in Redis.
/// </summary>
public interface IPresenceService
{
    /// <summary>
    /// Sets the presence status for a user.
    /// </summary>
    Task SetStatusAsync(long userId, PresenceStatus status);

    /// <summary>
    /// Gets the current presence status for a user.
    /// </summary>
    Task<PresenceStatus> GetStatusAsync(long userId);

    /// <summary>
    /// Gets the last seen timestamp for a user.
    /// </summary>
    Task<DateTime?> GetLastSeenAsync(long userId);

    /// <summary>
    /// Records a heartbeat for a user across their servers.
    /// </summary>
    Task HeartbeatAsync(long userId, IEnumerable<long> serverIds);

    /// <summary>
    /// Gets presence status for multiple users in bulk.
    /// </summary>
    Task<Dictionary<long, PresenceStatus>> GetBulkStatusAsync(IEnumerable<long> userIds);

    /// <summary>
    /// Removes presence data for a user from specified servers.
    /// </summary>
    Task RemovePresenceAsync(long userId, IEnumerable<long> serverIds);
}
