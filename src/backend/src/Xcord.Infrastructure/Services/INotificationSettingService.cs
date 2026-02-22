using Xcord.Entities;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for resolving and evaluating notification settings.
/// </summary>
public interface INotificationSettingService
{
    /// <summary>
    /// Resolves the effective notification level for a user in a given scope.
    /// Resolution order: Channel > Server > Global (most specific wins).
    /// </summary>
    Task<NotificationLevel> ResolveAsync(long userId, long? serverId, long? channelId);

    /// <summary>
    /// Determines whether a user should receive a notification based on their settings.
    /// </summary>
    Task<bool> ShouldNotifyAsync(long userId, long? serverId, long? channelId, bool isEveryone, bool isRoleMention);
}
