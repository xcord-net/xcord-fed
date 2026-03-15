using Xcord.Entities;

namespace Xcord.Features.Messages;

public static class MessageOutboxPayloads
{
    public static object ForCreated(Message message, string? authorUsername, string? authorAvatarUrl, DateTimeOffset? editedAt = null, IReadOnlyList<object>? attachments = null)
    {
        return new
        {
            conversationId = message.ConversationId,
            id = message.Id,
            authorId = message.AuthorId,
            authorUsername,
            authorAvatarUrl,
            type = message.Type.ToString(),
            content = message.Content,
            metadata = message.Metadata,
            replyToId = message.ReplyToId,
            isPinned = message.IsPinned,
            editedAt,
            createdAt = message.CreatedAt,
            attachments
        };
    }
}
