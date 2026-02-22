using Xcord.Entities;
using Xcord.Infrastructure.Services;
using Xcord;

namespace Xcord.Features.Messages;

/// <summary>
/// Message processing pipeline service.
/// Processes message content through sanitization, validation, automod, mention parsing, and URL extraction.
/// </summary>
public interface IMessageProcessor
{
    /// <summary>
    /// Processes a message through the full pipeline.
    /// Returns the processed message with mentions populated.
    /// </summary>
    /// <param name="message">The message to process (content will be modified).</param>
    /// <param name="serverId">The server ID for permission checks.</param>
    /// <param name="channelId">The channel ID for automod exemptions.</param>
    /// <param name="authorRoleIds">The role IDs the author has in this server.</param>
    /// <param name="isBot">Whether the author is a bot.</param>
    /// <returns>Result containing the processed message and optional automod deferred actions, or an error.</returns>
    Task<Result<MessageProcessingResult>> ProcessAsync(Message message, long serverId, long channelId, IEnumerable<long> authorRoleIds, bool isBot);
}

/// <summary>
/// Result of message processing pipeline.
/// </summary>
public sealed class MessageProcessingResult
{
    public Message Message { get; init; } = null!;
    public List<AutomodDeferredAction> DeferredActions { get; init; } = new();
}
