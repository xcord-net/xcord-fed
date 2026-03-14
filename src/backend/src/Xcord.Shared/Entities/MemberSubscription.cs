using Xcord;

namespace Xcord.Entities;

public sealed class MemberSubscription : ISoftDeletable
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ServerId { get; set; }
    public long TierId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripeCustomerId { get; set; }
    public MemberSubscriptionStatus Status { get; set; }
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Server Server { get; set; } = null!;
    public Tier Tier { get; set; } = null!;
}

public enum MemberSubscriptionStatus
{
    Active = 0,
    PastDue = 1,
    Cancelled = 2,
    Expired = 3
}
