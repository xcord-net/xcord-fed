using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Billing;

public sealed record DeleteTierCommand(long ServerId, long TierId);

public sealed record DeleteTierResponse(string Message);

public sealed class DeleteTierHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IOptions<TierOptions> tierOptions)
    : IRequestHandler<DeleteTierCommand, Result<DeleteTierResponse>>
{
    public async Task<Result<DeleteTierResponse>> Handle(
        DeleteTierCommand request, CancellationToken cancellationToken)
    {
        if (!tierOptions.Value.CanUseMemberTiers)
            return Error.Forbidden("TIER_GATE", "Member-tier billing requires Pro tier or higher");

        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var server = await dbContext.Servers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ServerId, cancellationToken);

        if (server == null)
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        if (server.OwnerId != userId)
            return Error.Forbidden("NOT_OWNER", "Only the server owner can manage subscription tiers");

        var tier = await dbContext.Tiers
            .FirstOrDefaultAsync(t => t.Id == request.TierId && t.ServerId == request.ServerId, cancellationToken);

        if (tier == null)
            return Error.NotFound("TIER_NOT_FOUND", "Subscription tier not found");

        // Soft delete
        tier.SoftDelete();
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeleteTierResponse("Subscription tier deleted");
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/tiers/{tierId}", async (
            [FromRoute] long serverId,
            [FromRoute] long tierId,
            IRequestHandler<DeleteTierCommand, Result<DeleteTierResponse>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new DeleteTierCommand(serverId, tierId), ct);
        })
        .RequireAuthorization(Policies.User)
        .WithTags("Billing")
        .WithName("DeleteTier")
        .Produces<DeleteTierResponse>(200);
    }
}
