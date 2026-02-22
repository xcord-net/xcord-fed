namespace Xcord.Features.Federation;

public sealed record FederationFollowDto(
    long Id,
    string RemoteInstanceUrl,
    long LocalChannelId,
    string RemoteChannelId,
    string? RemoteChannelName,
    long FollowedByUserId,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public sealed record FederationInboxMessageDto(
    string RemoteMessageId,
    string AuthorName,
    string? AuthorAvatarUrl,
    string Content,
    string? Metadata,
    DateTimeOffset CreatedAt
);
