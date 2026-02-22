namespace Xcord.Features.ScheduledMessages;

public sealed record ScheduledMessageDto(
    long Id,
    long ConversationId,
    long AuthorId,
    string Content,
    string? Metadata,
    DateTimeOffset ScheduledAt,
    bool IsSent,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt
);
