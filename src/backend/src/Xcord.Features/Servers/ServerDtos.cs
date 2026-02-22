namespace Xcord.Features.Servers;

public sealed record CreateServerResponse(
    long Id,
    string Name,
    string? Description,
    string? IconUrl,
    string? BannerUrl,
    long OwnerId,
    int MemberCount,
    string? PreferredLocale,
    DateTimeOffset CreatedAt
);

public sealed record ServerDto(
    long Id,
    string Name,
    string? Description,
    string? IconUrl,
    string? BannerUrl,
    long OwnerId,
    int MemberCount,
    string? PreferredLocale,
    DateTimeOffset CreatedAt
);

public sealed record UpdateServerRequest(
    string? Name,
    string? Description,
    string? IconUrl,
    string? BannerUrl,
    string? PreferredLocale
);

public sealed record InviteDto(
    string Code,
    long ServerId,
    long? CreatedByUserId,
    int? MaxUses,
    int Uses,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt
);

public sealed record CreateInviteRequest(
    int? MaxUses,
    DateTimeOffset? ExpiresAt
);
