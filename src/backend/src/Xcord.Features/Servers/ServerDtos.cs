namespace Xcord.Features.Servers;

/// <param name="SystemChannelId">
///   The general channel created alongside the server - where a client should
///   land the owner.
///
///   Creating a server used to answer without it, so the only way to open the
///   thing you had just made was to fetch its channel list and look for one
///   named "general". That second round trip sat between the click and any
///   visible change, and the handler knew the id the whole time.
/// </param>
public sealed record CreateServerResponse(
    long Id,
    string Name,
    string? Description,
    string? IconUrl,
    string? BannerUrl,
    long OwnerId,
    int MemberCount,
    string? PreferredLocale,
    DateTimeOffset CreatedAt,
    long SystemChannelId
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
    DateTimeOffset CreatedAt,
    long? GroupId,
    long? ChannelId
);

public sealed record CreateInviteRequest(
    int? MaxUses,
    DateTimeOffset? ExpiresAt,
    long? GroupId,
    long? ChannelId
);

public sealed record JoinByInviteResponse(
    long Id,
    string Name,
    string? Description,
    string? IconUrl,
    string? BannerUrl,
    long OwnerId,
    int MemberCount,
    string? PreferredLocale,
    DateTimeOffset CreatedAt,
    long? ChannelId
);
