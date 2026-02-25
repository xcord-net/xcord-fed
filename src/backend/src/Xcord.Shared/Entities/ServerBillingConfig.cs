namespace Xcord.Entities;

public sealed class ServerBillingConfig
{
    public long Id { get; set; }
    public long ServerId { get; set; }
    public string? StripeConnectedAccountId { get; set; }
    public int RevenueSharePercent { get; set; } = 70;
    public bool PayoutEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    // Navigation properties
    public Server Server { get; set; } = null!;
}
