namespace Xcord.Infrastructure.Options;

public sealed class MemberBillingOptions
{
    public const string SectionName = "MemberBilling";

    /// <summary>
    /// Stripe secret key for member billing (from hub's Stripe Connect platform).
    /// When empty, member billing is disabled.
    /// </summary>
    public string StripeSecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe webhook secret for member billing events.
    /// </summary>
    public string StripeWebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Minimum platform cut percentage (0-100). Instance owners cannot reduce below this.
    /// </summary>
    public int MinPlatformCutPercent { get; set; } = 30;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(StripeSecretKey);
}
