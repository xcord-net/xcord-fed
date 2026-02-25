using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Billing;

public sealed record CreateSubscriptionTierCommand(
    long ServerId,
    string Name,
    string? Description,
    int PriceMonthly,
    string Currency,
    long[] RoleIds
);

public sealed class CreateSubscriptionTierHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<CreateSubscriptionTierCommand, Result<SubscriptionTierDto>>,
      IValidatable<CreateSubscriptionTierCommand>
{
    public Error? Validate(CreateSubscriptionTierCommand request)
    {
        if (request.ServerId <= 0)
            return Error.Validation("VALIDATION_ERROR", "ServerId is required");
        if (string.IsNullOrWhiteSpace(request.Name))
            return Error.Validation("VALIDATION_ERROR", "Name is required");
        if (request.Name.Length > 100)
            return Error.Validation("VALIDATION_ERROR", "Name must not exceed 100 characters");
        if (request.PriceMonthly < 100)
            return Error.Validation("VALIDATION_ERROR", "Price must be at least $1.00 (100 cents)");
        if (request.PriceMonthly > 1_000_00)
            return Error.Validation("VALIDATION_ERROR", "Price must not exceed $1000.00");
        return null;
    }

    public async Task<Result<SubscriptionTierDto>> Handle(
        CreateSubscriptionTierCommand request, CancellationToken cancellationToken)
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

        var existingCount = await dbContext.MemberSubscriptionTiers
            .CountAsync(t => t.ServerId == request.ServerId, cancellationToken);

        if (existingCount >= 10)
            return Error.BadRequest("MAX_TIERS", "Maximum of 10 subscription tiers per server");

        var now = DateTimeOffset.UtcNow;
        var tier = new MemberSubscriptionTier
        {
            Id = snowflakeGenerator.NextId(),
            ServerId = request.ServerId,
            Name = request.Name,
            Description = request.Description,
            PriceMonthly = request.PriceMonthly,
            Currency = request.Currency,
            RoleIdsJson = JsonSerializer.Serialize(request.RoleIds),
            IsActive = true,
            Position = existingCount,
            CreatedAt = now
        };

        dbContext.MemberSubscriptionTiers.Add(tier);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubscriptionTierDto(
            Id: tier.Id.ToString(),
            ServerId: tier.ServerId.ToString(),
            Name: tier.Name,
            Description: tier.Description,
            PriceMonthly: tier.PriceMonthly,
            Currency: tier.Currency,
            RoleIds: request.RoleIds,
            IsActive: tier.IsActive,
            Position: tier.Position
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/subscription-tiers", async (
            [FromRoute] long serverId,
            [FromBody] CreateSubscriptionTierRequest request,
            IRequestHandler<CreateSubscriptionTierCommand, Result<SubscriptionTierDto>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateSubscriptionTierCommand(
                ServerId: serverId,
                Name: request.Name,
                Description: request.Description,
                PriceMonthly: request.PriceMonthly,
                Currency: request.Currency ?? "usd",
                RoleIds: request.RoleIds ?? []
            );
            return await handler.ExecuteAsync(command, ct,
                tier => Results.Created($"/api/v1/servers/{serverId}/subscription-tiers/{tier.Id}", tier));
        })
        .RequireAuthorization(Policies.User)
        .WithTags("Billing")
        .WithName("CreateSubscriptionTier")
        .Produces<SubscriptionTierDto>(StatusCodes.Status201Created);
    }
}
