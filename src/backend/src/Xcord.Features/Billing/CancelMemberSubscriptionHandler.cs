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
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Billing;

public sealed record CancelMemberSubscriptionCommand(long ServerId);

public sealed record CancelMemberSubscriptionResponse(string Message);

public sealed class CancelMemberSubscriptionHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IOptions<MemberBillingOptions> billingOptions,
    IMemberBillingService billingService)
    : IRequestHandler<CancelMemberSubscriptionCommand, Result<CancelMemberSubscriptionResponse>>
{
    public async Task<Result<CancelMemberSubscriptionResponse>> Handle(
        CancelMemberSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var subscription = await dbContext.MemberSubscriptions
            .Include(s => s.Tier)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ServerId == request.ServerId
                && s.Status == MemberSubscriptionStatus.Active, cancellationToken);

        if (subscription == null)
            return Error.NotFound("NO_SUBSCRIPTION", "No active subscription found");

        // Cancel Stripe subscription if exists
        if (billingOptions.Value.IsConfigured && !string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
        {
            await billingService.CancelSubscriptionAsync(subscription.StripeSubscriptionId, cancellationToken);
        }

        subscription.Status = MemberSubscriptionStatus.Cancelled;
        subscription.CancelledAt = DateTimeOffset.UtcNow;

        // Remove tier roles
        var roleIds = JsonSerializer.Deserialize<long[]>(subscription.Tier.RoleIdsJson) ?? [];
        foreach (var roleId in roleIds)
        {
            var memberRole = await dbContext.MemberRoles
                .FirstOrDefaultAsync(mr => mr.UserId == userId && mr.ServerId == request.ServerId && mr.RoleId == roleId,
                    cancellationToken);

            if (memberRole != null)
                dbContext.MemberRoles.Remove(memberRole);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CancelMemberSubscriptionResponse("Subscription cancelled");
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/subscription/cancel", async (
            [FromRoute] long serverId,
            IRequestHandler<CancelMemberSubscriptionCommand, Result<CancelMemberSubscriptionResponse>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new CancelMemberSubscriptionCommand(serverId), ct);
        })
        .RequireAuthorization(Policies.User)
        .WithTags("Billing")
        .WithName("CancelMemberSubscription")
        .Produces<CancelMemberSubscriptionResponse>(200);
    }
}
