using Xcord.Entities;
using Xcord;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Result object containing resolved conversation context.
/// </summary>
public sealed record ConversationContext(
    long ConversationId,
    ConversationType Type,
    long ServerId,    // 0 for DMs
    long ChannelId    // 0 for DMs
);

/// <summary>
/// Service for resolving conversations and verifying membership/permissions.
/// </summary>
public interface IConversationResolver
{
    /// <summary>
    /// Resolves a conversation, verifies membership, and optionally checks a permission.
    /// Returns a ConversationContext with resolved server/channel IDs.
    /// </summary>
    Task<Result<ConversationContext>> ResolveAsync(
        long conversationId,
        long userId,
        Role? requiredRole = null,
        CancellationToken cancellationToken = default);
}
