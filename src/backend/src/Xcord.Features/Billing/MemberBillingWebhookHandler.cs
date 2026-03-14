using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;

namespace Xcord.Features.Billing;

public sealed class MemberBillingWebhookHandler(
    AppDbContext dbContext,
    IOptions<MemberBillingOptions> billingOptions,
    ILogger<MemberBillingWebhookHandler> logger)
{
    public async Task<IResult> HandleAsync(HttpContext httpContext, CancellationToken ct)
    {
        var options = billingOptions.Value;
        if (!options.IsConfigured)
            return Results.StatusCode(503);

        var json = await new StreamReader(httpContext.Request.Body).ReadToEndAsync(ct);
        Event stripeEvent;

        try
        {
            stripeEvent = !string.IsNullOrWhiteSpace(options.StripeWebhookSecret)
                ? EventUtility.ConstructEvent(json, httpContext.Request.Headers["Stripe-Signature"], options.StripeWebhookSecret)
                : EventUtility.ParseEvent(json);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Member billing webhook signature verification failed");
            return Results.BadRequest("Invalid signature");
        }

        logger.LogInformation("Processing member billing webhook {EventType} ({EventId})", stripeEvent.Type, stripeEvent.Id);

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await HandleCheckoutCompleted(stripeEvent, ct);
                break;

            case EventTypes.InvoicePaymentFailed:
                await HandlePaymentFailed(stripeEvent, ct);
                break;

            case EventTypes.CustomerSubscriptionDeleted:
                await HandleSubscriptionDeleted(stripeEvent, ct);
                break;

            default:
                logger.LogDebug("Unhandled member billing event: {EventType}", stripeEvent.Type);
                break;
        }

        return Results.Ok();
    }

    private async Task HandleCheckoutCompleted(Event stripeEvent, CancellationToken ct)
    {
        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
        if (session == null) return;

        if (!session.Metadata.TryGetValue("server_id", out var serverIdStr) ||
            !long.TryParse(serverIdStr, out var serverId) ||
            !session.Metadata.TryGetValue("tier_id", out var tierIdStr) ||
            !long.TryParse(tierIdStr, out var tierId))
        {
            logger.LogWarning("Member billing checkout {SessionId} missing metadata", session.Id);
            return;
        }

        // Find the subscription we need to update by finding a pending one or creating one
        var subscription = await dbContext.MemberSubscriptions
            .FirstOrDefaultAsync(s => s.ServerId == serverId && s.TierId == tierId
                && s.StripeSubscriptionId == null
                && s.Status != MemberSubscriptionStatus.Cancelled, ct);

        if (subscription != null)
        {
            subscription.StripeSubscriptionId = session.SubscriptionId;
            subscription.StripeCustomerId = session.CustomerId;
            subscription.Status = MemberSubscriptionStatus.Active;
            await dbContext.SaveChangesAsync(ct);
        }

        logger.LogInformation("Member billing checkout completed for server {ServerId}, tier {TierId}", serverId, tierId);
    }

    private async Task HandlePaymentFailed(Event stripeEvent, CancellationToken ct)
    {
        var invoice = stripeEvent.Data.Object as Stripe.Invoice;
        var subscriptionId = invoice?.Parent?.SubscriptionDetails?.SubscriptionId;
        if (subscriptionId == null) return;

        var subscription = await dbContext.MemberSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId, ct);

        if (subscription == null) return;

        subscription.Status = MemberSubscriptionStatus.PastDue;
        await dbContext.SaveChangesAsync(ct);

        logger.LogWarning("Member payment failed for subscription {SubscriptionId}", subscriptionId);
    }

    private async Task HandleSubscriptionDeleted(Event stripeEvent, CancellationToken ct)
    {
        var subscription = stripeEvent.Data.Object as Stripe.Subscription;
        if (subscription == null) return;

        var memberSub = await dbContext.MemberSubscriptions
            .Include(s => s.Tier)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id, ct);

        if (memberSub == null) return;

        memberSub.Status = MemberSubscriptionStatus.Cancelled;
        memberSub.CancelledAt = DateTimeOffset.UtcNow;

        // Remove tier groups
        var groupIds = JsonSerializer.Deserialize<long[]>(memberSub.Tier.GroupIdsJson) ?? [];
        foreach (var groupId in groupIds)
        {
            var memberGroup = await dbContext.MemberGroups
                .FirstOrDefaultAsync(mg => mg.UserId == memberSub.UserId && mg.ServerId == memberSub.ServerId && mg.GroupId == groupId, ct);

            if (memberGroup != null)
                dbContext.MemberGroups.Remove(memberGroup);
        }

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Member subscription {SubscriptionId} deleted, groups removed", subscription.Id);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/billing/webhook", async (
            MemberBillingWebhookHandler handler,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await handler.HandleAsync(httpContext, ct);
        })
        .WithName("MemberBillingWebhook")
        .WithTags("Billing");
    }
}
