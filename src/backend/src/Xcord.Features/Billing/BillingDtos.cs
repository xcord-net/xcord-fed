using Xcord.Entities;

namespace Xcord.Features.Billing;

public sealed record SubscriptionTierDto(
    string Id,
    string ServerId,
    string Name,
    string? Description,
    int PriceMonthly,
    string Currency,
    long[] RoleIds,
    bool IsActive,
    int Position
);

public sealed record CreateSubscriptionTierRequest(
    string Name,
    string? Description,
    int PriceMonthly,
    string? Currency,
    long[]? RoleIds
);

public sealed record UpdateSubscriptionTierRequest(
    string? Name,
    string? Description,
    int? PriceMonthly,
    long[]? RoleIds,
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
