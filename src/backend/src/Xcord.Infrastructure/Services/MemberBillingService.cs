using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

public sealed class MemberBillingService(IOptions<MemberBillingOptions> options) : IMemberBillingService
{
    private StripeClient GetClient() => new(options.Value.StripeSecretKey);

    public async Task<string> EnsureCustomerAsync(long userId, string email, string? displayName, CancellationToken ct)
    {
        var client = GetClient();
        var service = new CustomerService(client);

        var existing = await service.ListAsync(new CustomerListOptions
        {
            Email = email,
            Limit = 1
        }, cancellationToken: ct);

        if (existing.Data.Count > 0)
            return existing.Data[0].Id;

        var customer = await service.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Name = displayName,
            Metadata = new Dictionary<string, string>
            {
                ["xcord_user_id"] = userId.ToString()
            }
        }, cancellationToken: ct);

        return customer.Id;
    }

    public async Task<MemberCheckoutResult> CreateSubscriptionCheckoutAsync(MemberCheckoutRequest request, CancellationToken ct)
    {
        var client = GetClient();
        var service = new SessionService(client);

        // Direct subscription billing through this instance's own Stripe account.
        // No Stripe Connect, no application_fee_percent, no platform cut.
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Customer = request.CustomerId,
            Mode = "subscription",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency,
                        UnitAmount = request.PriceAmountCents,
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = "month"
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Server Subscription"
                        }
                    },
                    Quantity = 1
                }
            ],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["server_id"] = request.ServerId.ToString(),
                    ["tier_id"] = request.TierId.ToString()
                }
            },
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["server_id"] = request.ServerId.ToString(),
                ["tier_id"] = request.TierId.ToString()
            }
        }, cancellationToken: ct);

        return new MemberCheckoutResult(session.Url, session.Id);
    }

    public async Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct)
    {
        var client = GetClient();
        var service = new SubscriptionService(client);
        await service.CancelAsync(stripeSubscriptionId, cancellationToken: ct);
    }
}
