using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Billing;

public sealed record GetMemberSubscriptionQuery(long ServerId);

public sealed class GetMemberSubscriptionHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<GetMemberSubscriptionQuery, Result<MemberSubscriptionDto>>
{
    public async Task<Result<MemberSubscriptionDto>> Handle(
        GetMemberSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var subscription = await dbContext.MemberSubscriptions
            .AsNoTracking()
            .Include(s => s.Tier)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ServerId == request.ServerId
                && (s.Status == MemberSubscriptionStatus.Active || s.Status == MemberSubscriptionStatus.PastDue),
                cancellationToken);

        if (subscription == null)
            return Error.NotFound("NO_SUBSCRIPTION", "No active subscription found");

        return new MemberSubscriptionDto(
            Id: subscription.Id.ToString(),
            ServerId: subscription.ServerId.ToString(),
            TierId: subscription.TierId.ToString(),
            TierName: subscription.Tier.Name,
            PriceMonthly: subscription.Tier.PriceMonthly,
            Status: subscription.Status.ToString(),
            CurrentPeriodEnd: subscription.CurrentPeriodEnd?.ToString("O")
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/subscription", async (
            [FromRoute] long serverId,
            IRequestHandler<GetMemberSubscriptionQuery, Result<MemberSubscriptionDto>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new GetMemberSubscriptionQuery(serverId), ct);
        })
        .RequireAuthorization(Policies.User)
        .WithTags("Billing")
        .WithName("GetMemberSubscription")
        .Produces<MemberSubscriptionDto>(200);
    }
}
