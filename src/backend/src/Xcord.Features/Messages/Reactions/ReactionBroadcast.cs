using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Messages;

/// <summary>
/// Tells everyone else in a conversation that a message's reactions changed.
/// </summary>
/// <remarks>
/// Adding or removing a reaction used to notify nobody. The person who reacted
/// refetched the message locally and saw the badge; every other browser in the
/// conversation kept the count it had loaded until something else made it
/// reload. Both handlers go through here so the two directions cannot drift.
///
/// The whole reaction set for the message is sent rather than a delta: it is a
/// handful of rows, it makes a removal and an addition the same message, and it
/// leaves no way for a client to end up with a count that never converges.
/// </remarks>
internal static class ReactionBroadcast
{
    public static async Task PublishAsync(
        AppDbContext dbContext,
        INotificationService notificationService,
        long conversationId,
        long messageId,
        CancellationToken ct)
    {
        var reactions = await dbContext.Reactions
            .AsNoTracking()
            .Where(r => r.MessageId == messageId)
            .GroupBy(r => r.Emoji)
            .Select(g => new
            {
                emoji = g.Key,
                count = g.Count(),
                userIds = g.Select(r => r.UserId).ToList(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        await notificationService.NotifyConversationAsync(
            conversationId,
            "Chat_ReactionsUpdated",
            new { conversationId, messageId, reactions },
            ct).ConfigureAwait(false);
    }
}
