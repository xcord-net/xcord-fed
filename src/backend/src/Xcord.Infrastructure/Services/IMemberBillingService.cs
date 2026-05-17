namespace Xcord.Infrastructure.Services;

public interface IMemberBillingService
{
    Task<string> EnsureCustomerAsync(long userId, string email, string? displayName, CancellationToken ct);
    Task<MemberCheckoutResult> CreateSubscriptionCheckoutAsync(MemberCheckoutRequest request, CancellationToken ct);
    Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct);
}

public sealed record MemberCheckoutRequest(
    string CustomerId,
    int PriceAmountCents,
    string Currency,
    long ServerId,
    long TierId,
    string SuccessUrl,
    string CancelUrl
);

public sealed record MemberCheckoutResult(string CheckoutUrl, string SessionId);
