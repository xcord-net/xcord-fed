namespace Xcord.Infrastructure.Services;

/// <summary>
/// Executes deferred automod actions after message persistence.
/// </summary>
public interface IAutomodActionExecutor
{
    /// <summary>
    /// Executes deferred automod actions for a message.
    /// </summary>
    /// <param name="messageId">The message ID that triggered the actions.</param>
    /// <param name="serverId">The server ID where the message was sent.</param>
    /// <param name="channelId">The channel ID where the message was sent.</param>
    /// <param name="authorId">The author ID of the message.</param>
    /// <param name="actions">The list of deferred actions to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteDeferredActionsAsync(
        long messageId,
        long serverId,
        long channelId,
        long authorId,
        List<AutomodDeferredAction> actions,
        CancellationToken cancellationToken = default);
}
