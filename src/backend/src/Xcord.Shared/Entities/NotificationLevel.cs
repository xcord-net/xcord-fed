namespace Xcord.Entities;

/// <summary>
/// Notification level for a user's notification settings.
/// </summary>
public enum NotificationLevel
{
    /// <summary>
    /// Receive all notifications.
    /// </summary>
    All = 0,

    /// <summary>
    /// Only receive notifications for mentions.
    /// </summary>
    MentionsOnly = 1,

    /// <summary>
    /// Do not receive any notifications.
    /// </summary>
    None = 2
}
