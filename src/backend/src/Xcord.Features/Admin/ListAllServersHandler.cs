using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Admin;

public sealed record ListAllServersQuery;

public sealed record AdminServerSummary(long Id, string Name, long OwnerId, DateTimeOffset CreatedAt);

public sealed class ListAllServersHandler(AppDbContext dbContext)
    : IRequestHandler<ListAllServersQuery, Result<List<AdminServerSummary>>>
{
    public async Task<Result<List<AdminServerSummary>>> Handle(
        ListAllServersQuery request, CancellationToken cancellationToken)
    {
        var servers = await dbContext.Servers
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => new AdminServerSummary(s.Id, s.Name, s.OwnerId, s.CreatedAt))
            .ToListAsync(cancellationToken);

        return servers;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/admin/servers", async (
            ListAllServersHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new ListAllServersQuery(), ct).ConfigureAwait(false);
        })
        .RequireAuthorization(Policies.Admin)
        .Produces<List<AdminServerSummary>>(200)
        .WithName("ListAllServersAdmin")
        .WithTags("Admin", "Servers");
    }
}
