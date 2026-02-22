namespace Xcord.Features.Dms;

/// <summary>
/// Response DTO for DM channel creation.
/// </summary>
public sealed record CreateDmResponse(
    long Id,
    long ConversationId,
    bool IsGroup,
    string? Name,
    long? OwnerId,
    DmMemberDto[] Members
);

/// <summary>
/// Response DTO for DM channel details.
/// </summary>
public sealed record DmChannelDto(
    long Id,
    long ConversationId,
    bool IsGroup,
    string? Name,
    long? OwnerId,
    DmMemberDto[] Members,
    MessagePreviewDto? LastMessage
);

/// <summary>
/// DTO for DM channel member.
/// </summary>
public sealed record DmMemberDto(
    long UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    DateTimeOffset JoinedAt
);

/// <summary>
/// DTO for last message preview.
/// </summary>
public sealed record MessagePreviewDto(
    long MessageId,
    long? AuthorId,
    string Content,
    DateTimeOffset CreatedAt
);
