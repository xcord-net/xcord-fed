namespace Xcord.Features.Admin;

/// <summary>
/// Response DTOs for bot management endpoints.
/// </summary>

public sealed record CreateBotResponse(
    long UserId,
    string Username,
    string DisplayName,
    long TokenId,
    string TokenName,
    string RawToken, // Only returned once on creation
    long Permissions,
    DateTimeOffset CreatedAt
);

public sealed record BotTokenDto(
    long TokenId,
    string TokenName,
    long UserId,
    string Username,
    long Permissions,
    bool IsRevoked,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt
);

public sealed record ListBotsResponse(
    BotTokenDto[] Bots
);

