using Xcord.Entities;

namespace Xcord.Features.Billing;

public sealed record TierDto(
    string Id,
    string ServerId,
    string Name,
    string? Description,
    int PriceMonthly,
    string Currency,
    long[] GroupIds,
    bool IsActive,
    int Position
);

public sealed record CreateTierRequest(
    string Name,
    string? Description,
    int PriceMonthly,
    string? Currency,
    long[]? GroupIds
);

public sealed record UpdateTierRequest(
    string? Name,
    string? Description,
    int? PriceMonthly,
    long[]? GroupIds,
    bool? IsActive
);

public sealed record MemberSubscriptionDto(
    string Id,
    string ServerId,
    string TierId,
    string TierName,
    int PriceMonthly,
    string Status,
    string? CurrentPeriodEnd
);

public sealed record ServerBillingConfigDto(
    string ServerId,
    string? StripeConnectedAccountId,
    int RevenueSharePercent,
    bool PayoutEnabled,
    bool IsConnected
);
