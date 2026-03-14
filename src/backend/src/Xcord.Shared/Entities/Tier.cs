using Xcord;

namespace Xcord.Entities;

public sealed class Tier : ISoftDeletable
{
    public long Id { get; set; }
    public long ServerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PriceMonthly { get; set; }
    public string Currency { get; set; } = "usd";
    public string GroupIdsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public int Position { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Server Server { get; set; } = null!;
    public ICollection<MemberSubscription> Subscriptions { get; set; } = new List<MemberSubscription>();
}
