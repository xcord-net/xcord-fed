using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Billing;

public sealed record UpdateSubscriptionTierCommand(
    long ServerId,
    long TierId,
    string? Name,
    string? Description,
    int? PriceMonthly,
    long[]? RoleIds,
    bool? IsActive
);

public sealed class UpdateSubscriptionTierHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateSubscriptionTierCommand, Result<SubscriptionTierDto>>
{
    public async Task<Result<SubscriptionTierDto>> Handle(
        UpdateSubscriptionTierCommand request, CancellationToken cancellationToken)
    {
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

        var tier = await dbContext.MemberSubscriptionTiers
            .FirstOrDefaultAsync(t => t.Id == request.TierId && t.ServerId == request.ServerId, cancellationToken);

        if (tier == null)
            return Error.NotFound("TIER_NOT_FOUND", "Subscription tier not found");

        if (request.Name != null) tier.Name = request.Name;
        if (request.Description != null) tier.Description = request.Description;
        if (request.PriceMonthly.HasValue) tier.PriceMonthly = request.PriceMonthly.Value;
        if (request.RoleIds != null) tier.RoleIdsJson = JsonSerializer.Serialize(request.RoleIds);
        if (request.IsActive.HasValue) tier.IsActive = request.IsActive.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        var roleIds = JsonSerializer.Deserialize<long[]>(tier.RoleIdsJson) ?? [];
        return new SubscriptionTierDto(
            Id: tier.Id.ToString(),
            ServerId: tier.ServerId.ToString(),
            Name: tier.Name,
            Description: tier.Description,
            PriceMonthly: tier.PriceMonthly,
            Currency: tier.Currency,
            RoleIds: roleIds,
            IsActive: tier.IsActive,
            Position: tier.Position
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/servers/{serverId}/subscription-tiers/{tierId}", async (
            [FromRoute] long serverId,
            [FromRoute] long tierId,
            [FromBody] UpdateSubscriptionTierRequest request,
            IRequestHandler<UpdateSubscriptionTierCommand, Result<SubscriptionTierDto>> handler,
            CancellationToken ct) =>
        {
            var command = new UpdateSubscriptionTierCommand(
                ServerId: serverId,
                TierId: tierId,
                Name: request.Name,
                Description: request.Description,
                PriceMonthly: request.PriceMonthly,
                RoleIds: request.RoleIds,
                IsActive: request.IsActive
            );
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAuthorization(Policies.User)
        .WithTags("Billing")
        .WithName("UpdateSubscriptionTier")
        .Produces<SubscriptionTierDto>(200);
    }
}
