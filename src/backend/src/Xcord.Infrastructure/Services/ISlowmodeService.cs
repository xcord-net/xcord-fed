namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for enforcing per-channel slowmode rate limits using Redis.
/// Tracks last-message timestamps per user per channel.
/// </summary>
public interface ISlowmodeService
{
    /// <summary>
    /// Checks whether the user is currently in a slowmode cooldown for the given channel.
    /// If not rate-limited, records the current timestamp so the next call can enforce the interval.
    /// </summary>
    /// <param name="userId">The user attempting to send a message.</param>
    /// <param name="channelId">The channel the message is being sent to.</param>
    /// <param name="slowModeSeconds">The configured slowmode interval in seconds.</param>
    /// <returns>
    /// 0 if the message is allowed (and the timestamp has been recorded).
    /// A positive integer representing the remaining cooldown seconds if rate-limited.
    /// </returns>
    Task<int> CheckAndRecordAsync(long userId, long channelId, int slowModeSeconds);
}
