namespace Xcord.Features.Messages;

public sealed record GetReactionsResponse(
    List<ReactionGroupDto> Reactions
);

public sealed record ReactionGroupDto(
    string Emoji,
    int Count,
    List<ReactionUserDto> Users
);

public sealed record ReactionUserDto(
    long UserId,
    string Username,
    string? AvatarUrl
);
