namespace Xcord.Infrastructure.Options;

public sealed class MemberBillingOptions
{
    public const string SectionName = "MemberBilling";

    /// <summary>
    /// Stripe secret key for this instance's own Stripe account.
    /// Member subscriptions are billed directly to this account.
    /// When empty, member billing is disabled (subscriptions apply locally for dev).
    /// </summary>
    public string StripeSecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe webhook secret for member billing events.
    /// </summary>
    public string StripeWebhookSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(StripeSecretKey);
}
