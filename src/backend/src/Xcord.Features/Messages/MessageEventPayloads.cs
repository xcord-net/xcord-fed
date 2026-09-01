using Xcord.Entities;

namespace Xcord.Features.Messages;

public static class MessageEventPayloads
{
    /// <summary>
    /// Payload for a system message (member joined, left, was kicked or banned).
    /// </summary>
    /// <remarks>
    /// Uses the same field names as <see cref="ForCreated"/>. These broadcasts used to
    /// send a <c>messageId</c> instead of an <c>id</c>, so the client appended a
    /// message with an undefined id and no content - a blank "Unknown User" row that
    /// stayed in the list until reload. The type and metadata are what the client
    /// renders the system line from, so both have to travel.
    /// </remarks>
    public static object ForSystemCreated(Message message)
    {
        return new
        {
            conversationId = message.ConversationId,
            id = message.Id,
            authorId = message.AuthorId,
            authorUsername = (string?)null,
            authorAvatarUrl = (string?)null,
            authorGroupColor = (string?)null,
            type = message.Type.ToString(),
            content = message.Content,
            metadata = message.Metadata,
            replyToId = message.ReplyToId,
            replyTo = (ReplyToDto?)null,
            isPinned = message.IsPinned,
            editedAt = message.EditedAt,
            createdAt = message.CreatedAt,
            attachments = (IReadOnlyList<object>?)null
        };
    }

    public static object ForCreated(Message message, string? authorUsername, string? authorAvatarUrl, DateTimeOffset? editedAt = null, IReadOnlyList<object>? attachments = null, ReplyToDto? replyTo = null, string? authorGroupColor = null)
    {
        return new
        {
            conversationId = message.ConversationId,
            id = message.Id,
            authorId = message.AuthorId,
            authorUsername,
            authorAvatarUrl,
            authorGroupColor,
            type = message.Type.ToString(),
            content = message.Content,
            metadata = message.Metadata,
            replyToId = message.ReplyToId,
            replyTo,
            isPinned = message.IsPinned,
            editedAt,
            createdAt = message.CreatedAt,
            attachments
        };
    }
}
