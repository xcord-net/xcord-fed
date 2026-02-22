namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for checking if a user is timed out in a server.
/// </summary>
public interface ITimeoutService
{
    /// <summary>
    /// Checks if a user is currently timed out in a server.
    /// Checks Redis cache first, then database.
    /// </summary>
    /// <param name="userId">User ID to check.</param>
    /// <param name="serverId">Server ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user is currently timed out, false otherwise.</returns>
    Task<bool> IsTimedOutAsync(long userId, long serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a timeout for a user in Redis cache.
    /// </summary>
    /// <param name="userId">User ID to timeout.</param>
    /// <param name="serverId">Server ID.</param>
    /// <param name="expiresAt">Expiration timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetTimeoutAsync(long userId, long serverId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a timeout for a user from Redis cache.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="serverId">Server ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveTimeoutAsync(long userId, long serverId, CancellationToken cancellationToken = default);
}
