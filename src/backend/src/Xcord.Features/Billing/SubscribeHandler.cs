using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;
using IEncryptionService = Xcord.Infrastructure.Services.IEncryptionService;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Billing;

public sealed record SubscribeCommand(long ServerId, long TierId);

public sealed record SubscribeResponse(
    string? CheckoutUrl,
    bool RequiresCheckout,
    MemberSubscriptionDto? Subscription
);

public sealed class SubscribeHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IOptions<MemberBillingOptions> billingOptions,
    IOptions<InstanceOptions> instanceOptions,
    IMemberBillingService billingService,
    IEncryptionService encryptionService)
    : IRequestHandler<SubscribeCommand, Result<SubscribeResponse>>
{
    public async Task<Result<SubscribeResponse>> Handle(
        SubscribeCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var server = await dbContext.Servers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ServerId, cancellationToken);

        if (server == null)
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        // Check member is in the server
        var isMember = await dbContext.ServerMembers
            .AnyAsync(m => m.UserId == userId && m.ServerId == request.ServerId, cancellationToken);

        if (!isMember)
            return Error.Forbidden("NOT_MEMBER", "You must be a member of this server to subscribe");

        var tier = await dbContext.Tiers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TierId && t.ServerId == request.ServerId && t.IsActive,
                cancellationToken);

        if (tier == null)
            return Error.NotFound("TIER_NOT_FOUND", "Subscription tier not found or inactive");

        // Check for existing active subscription in this server
        var existing = await dbContext.MemberSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ServerId == request.ServerId
                && s.Status == MemberSubscriptionStatus.Active, cancellationToken);

        if (existing != null)
            return Error.BadRequest("ALREADY_SUBSCRIBED", "You already have an active subscription in this server");

        var options = billingOptions.Value;
        var billingConfig = await dbContext.ServerBillingConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServerId == request.ServerId, cancellationToken);

        // If Stripe is configured and server has connected account, create checkout
        if (options.IsConfigured && billingConfig?.StripeConnectedAccountId != null)
        {
            var user = await dbContext.Set<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
                return Error.NotFound("USER_NOT_FOUND", "User not found");

            var email = encryptionService.Decrypt(user.Email);
            var customerId = await billingService.EnsureCustomerAsync(
                userId, email, user.DisplayName, cancellationToken);

            var baseUrl = instanceOptions.Value.Domain;
            var platformFee = 100 - (billingConfig.RevenueSharePercent);

            var checkout = await billingService.CreateSubscriptionCheckoutAsync(
                new MemberCheckoutRequest(
                    CustomerId: customerId,
                    ConnectedAccountId: billingConfig.StripeConnectedAccountId,
                    PriceAmountCents: tier.PriceMonthly,
                    Currency: tier.Currency,
                    ServerId: request.ServerId,
                    TierId: request.TierId,
                    PlatformFeePercent: platformFee,
                    SuccessUrl: $"https://{baseUrl}/servers/{request.ServerId}/billing?checkout=success",
                    CancelUrl: $"https://{baseUrl}/servers/{request.ServerId}/billing?checkout=cancelled"
                ), cancellationToken);

            return new SubscribeResponse(
                CheckoutUrl: checkout.CheckoutUrl,
                RequiresCheckout: true,
                Subscription: null
            );
        }

        // No Stripe: apply subscription directly (dev/self-hosted)
        var now = DateTimeOffset.UtcNow;
        var subscription = new MemberSubscription
        {
            Id = snowflakeGenerator.NextId(),
            UserId = userId,
            ServerId = request.ServerId,
            TierId = request.TierId,
            Status = MemberSubscriptionStatus.Active,
            CurrentPeriodEnd = now.AddMonths(1),
            CreatedAt = now
        };

        dbContext.MemberSubscriptions.Add(subscription);

        // Auto-assign tier groups
        var groupIds = JsonSerializer.Deserialize<long[]>(tier.GroupIdsJson) ?? [];
        foreach (var groupId in groupIds)
        {
            var groupExists = await dbContext.Groups.AnyAsync(g => g.Id == groupId && g.ServerId == request.ServerId, cancellationToken);
            if (!groupExists) continue;

            var alreadyHasGroup = await dbContext.MemberGroups
                .AnyAsync(mg => mg.UserId == userId && mg.ServerId == request.ServerId && mg.GroupId == groupId, cancellationToken);

            if (!alreadyHasGroup)
            {
                dbContext.MemberGroups.Add(new MemberGroup
                {
                    UserId = userId,
                    ServerId = request.ServerId,
                    GroupId = groupId
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubscribeResponse(
            CheckoutUrl: null,
            RequiresCheckout: false,
            Subscription: new MemberSubscriptionDto(
                Id: subscription.Id.ToString(),
                ServerId: subscription.ServerId.ToString(),
                TierId: subscription.TierId.ToString(),
                TierName: tier.Name,
                PriceMonthly: tier.PriceMonthly,
                Status: subscription.Status.ToString(),
                CurrentPeriodEnd: subscription.CurrentPeriodEnd?.ToString("O")
            )
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/subscribe", async (
            [FromRoute] long serverId,
            [FromBody] SubscribeRequest request,
            IRequestHandler<SubscribeCommand, Result<SubscribeResponse>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new SubscribeCommand(serverId, request.TierId), ct);
        })
        .RequireAuthorization(Policies.User)
        .WithTags("Billing")
        .WithName("Subscribe")
        .Produces<SubscribeResponse>(200);
    }
}

public sealed record SubscribeRequest(long TierId);
