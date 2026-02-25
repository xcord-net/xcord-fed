using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Billing;

public sealed record DeleteSubscriptionTierCommand(long ServerId, long TierId);

public sealed record DeleteSubscriptionTierResponse(string Message);

public sealed class DeleteSubscriptionTierHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<DeleteSubscriptionTierCommand, Result<DeleteSubscriptionTierResponse>>
{
    public async Task<Result<DeleteSubscriptionTierResponse>> Handle(
        DeleteSubscriptionTierCommand request, CancellationToken cancellationToken)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var server = await dbContext.Servers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ServerId, cancellationToken);

        if (server == null)
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        if (server.OwnerId != userId)
            return Error.Forbidden("NOT_OWNER", "Only the server owner can manage subscription tiers");

        var tier = await dbContext.MemberSubscriptionTiers
            .FirstOrDefaultAsync(t => t.Id == request.TierId && t.ServerId == request.ServerId, cancellationToken);

        if (tier == null)
            return Error.NotFound("TIER_NOT_FOUND", "Subscription tier not found");

        // Soft delete
        tier.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeleteSubscriptionTierResponse("Subscription tier deleted");
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/subscription-tiers/{tierId}", async (
            [FromRoute] long serverId,
            [FromRoute] long tierId,
            IRequestHandler<DeleteSubscriptionTierCommand, Result<DeleteSubscriptionTierResponse>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new DeleteSubscriptionTierCommand(serverId, tierId), ct);
        })
        .RequireAuthorization(Policies.User)
        .WithTags("Billing")
        .WithName("DeleteSubscriptionTier")
        .Produces<DeleteSubscriptionTierResponse>(200);
    }
}
