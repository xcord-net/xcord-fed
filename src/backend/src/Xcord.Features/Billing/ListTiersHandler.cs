using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Billing;

public sealed record ListTiersQuery(long ServerId);

public sealed record ListTiersResponse(List<TierDto> Tiers);

public sealed class ListTiersHandler(AppDbContext dbContext)
    : IRequestHandler<ListTiersQuery, Result<ListTiersResponse>>
{
    public async Task<Result<ListTiersResponse>> Handle(
        ListTiersQuery request, CancellationToken cancellationToken)
    {
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        var tiers = await dbContext.Tiers
            .AsNoTracking()
            .Where(t => t.ServerId == request.ServerId && t.IsActive)
            .OrderBy(t => t.Position)
            .Select(t => new TierDto(
                t.Id.ToString(),
                t.ServerId.ToString(),
                t.Name,
                t.Description,
                t.PriceMonthly,
                t.Currency,
                JsonSerializer.Deserialize<long[]>(t.GroupIdsJson, (JsonSerializerOptions?)null) ?? Array.Empty<long>(),
                t.IsActive,
                t.Position
            ))
            .ToListAsync(cancellationToken);

        return new ListTiersResponse(tiers);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/tiers", async (
            [FromRoute] long serverId,
            IRequestHandler<ListTiersQuery, Result<ListTiersResponse>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new ListTiersQuery(serverId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Billing")
        .WithName("ListTiers")
        .Produces<ListTiersResponse>(200);
    }
}
