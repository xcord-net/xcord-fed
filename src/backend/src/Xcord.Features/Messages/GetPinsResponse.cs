using Xcord.Entities;

namespace Xcord.Features.Messages;

public sealed record GetPinsResponse(
    List<PinnedMessageDto> PinnedMessages
);

public sealed record PinnedMessageDto(
    long Id,
    long ConversationId,
    long? AuthorId,
    string? AuthorUsername,
    string? AuthorAvatarUrl,
    MessageType Type,
    string Content,
    string? Metadata,
    long? ReplyToId,
    DateTimeOffset? EditedAt,
    DateTimeOffset CreatedAt
);
