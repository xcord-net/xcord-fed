using System.Text.Json;
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

namespace Xcord.Features.Billing;

public sealed record UpdateTierCommand(
    long ServerId,
    long TierId,
    string? Name,
    string? Description,
    int? PriceMonthly,
    long[]? GroupIds,
    bool? IsActive
);

public sealed class UpdateTierHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IOptions<TierOptions> tierOptions)
    : IRequestHandler<UpdateTierCommand, Result<TierDto>>
{
    public async Task<Result<TierDto>> Handle(
        UpdateTierCommand request, CancellationToken cancellationToken)
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

        if (request.Name != null) tier.Name = request.Name;
        if (request.Description != null) tier.Description = request.Description;
        if (request.PriceMonthly.HasValue) tier.PriceMonthly = request.PriceMonthly.Value;
        if (request.GroupIds != null) tier.GroupIdsJson = JsonSerializer.Serialize(request.GroupIds);
        if (request.IsActive.HasValue) tier.IsActive = request.IsActive.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        var groupIds = JsonSerializer.Deserialize<long[]>(tier.GroupIdsJson) ?? [];
        return new TierDto(
            Id: tier.Id.ToString(),
            ServerId: tier.ServerId.ToString(),
            Name: tier.Name,
            Description: tier.Description,
            PriceMonthly: tier.PriceMonthly,
            Currency: tier.Currency,
            GroupIds: groupIds,
            IsActive: tier.IsActive,
            Position: tier.Position
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/servers/{serverId}/tiers/{tierId}", async (
            [FromRoute] long serverId,
            [FromRoute] long tierId,
            [FromBody] UpdateTierRequest request,
            IRequestHandler<UpdateTierCommand, Result<TierDto>> handler,
            CancellationToken ct) =>
        {
            var command = new UpdateTierCommand(
                ServerId: serverId,
                TierId: tierId,
                Name: request.Name,
                Description: request.Description,
                PriceMonthly: request.PriceMonthly,
                GroupIds: request.GroupIds,
                IsActive: request.IsActive
            );
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAuthorization(Policies.User)
        .WithTags("Billing")
        .WithName("UpdateTier")
        .Produces<TierDto>(200);
    }
}
