namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for evaluating automod rules against messages.
/// </summary>
public interface IAutomodService
{
    /// <summary>
    /// Evaluates automod rules for a message.
    /// Checks exemptions, evaluates enabled rules, and returns the result.
    /// </summary>
    /// <param name="messageContent">The message content to evaluate.</param>
    /// <param name="serverId">The server ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="authorId">The author user ID.</param>
    /// <param name="authorGroupIds">The group IDs the author has in this server.</param>
    /// <param name="isBot">Whether the author is a bot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Automod evaluation result.</returns>
    Task<AutomodResult> EvaluateAsync(
        string messageContent,
        long serverId,
        long channelId,
        long authorId,
        IEnumerable<long> authorGroupIds,
        bool isBot,
        CancellationToken cancellationToken = default);
}
