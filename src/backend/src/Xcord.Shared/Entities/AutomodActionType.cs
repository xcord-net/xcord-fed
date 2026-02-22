namespace Xcord.Entities;

/// <summary>
/// Types of automod actions to execute when a rule is triggered.
/// </summary>
public enum AutomodActionType
{
    /// <summary>
    /// Delete the message after it's been persisted.
    /// </summary>
    DeleteMessage = 0,

    /// <summary>
    /// Timeout the user (requires ActionConfig.timeoutDurationMinutes).
    /// </summary>
    TimeoutUser = 1,

    /// <summary>
    /// Send alert to moderators (requires ActionConfig.alertChannelId).
    /// </summary>
    AlertMods = 2,

    /// <summary>
    /// Block the message from being sent (pre-persist).
    /// </summary>
    BlockMessage = 3
}
