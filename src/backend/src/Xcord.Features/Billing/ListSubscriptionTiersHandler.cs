using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Billing;

public sealed record ListSubscriptionTiersQuery(long ServerId);

public sealed record ListSubscriptionTiersResponse(List<SubscriptionTierDto> Tiers);

public sealed class ListSubscriptionTiersHandler(AppDbContext dbContext)
    : IRequestHandler<ListSubscriptionTiersQuery, Result<ListSubscriptionTiersResponse>>
{
    public async Task<Result<ListSubscriptionTiersResponse>> Handle(
        ListSubscriptionTiersQuery request, CancellationToken cancellationToken)
    {
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        var tiers = await dbContext.MemberSubscriptionTiers
            .AsNoTracking()
            .Where(t => t.ServerId == request.ServerId && t.IsActive)
            .OrderBy(t => t.Position)
            .Select(t => new SubscriptionTierDto(
                t.Id.ToString(),
                t.ServerId.ToString(),
                t.Name,
                t.Description,
                t.PriceMonthly,
                t.Currency,
                JsonSerializer.Deserialize<long[]>(t.RoleIdsJson, (JsonSerializerOptions?)null) ?? Array.Empty<long>(),
                t.IsActive,
                t.Position
            ))
            .ToListAsync(cancellationToken);

        return new ListSubscriptionTiersResponse(tiers);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/subscription-tiers", async (
            [FromRoute] long serverId,
            IRequestHandler<ListSubscriptionTiersQuery, Result<ListSubscriptionTiersResponse>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new ListSubscriptionTiersQuery(serverId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Billing")
        .WithName("ListSubscriptionTiers")
        .Produces<ListSubscriptionTiersResponse>(200);
    }
}
