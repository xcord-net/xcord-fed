using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Admin.Migration;

public sealed record ListMigrationsQuery;

public sealed class ListMigrationsHandler(AppDbContext dbContext)
    : IRequestHandler<ListMigrationsQuery, Result<List<MigrationStatusResponse>>>
{
    public async Task<Result<List<MigrationStatusResponse>>> Handle(
        ListMigrationsQuery request, CancellationToken cancellationToken)
    {
        var migrations = await dbContext.DiscordMigrations
            .AsNoTracking()
            .Where(m => m.DeletedAt == null)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MigrationStatusResponse(
                m.Id,
                m.DiscordGuildId,
                m.ServerId,
                m.Status,
                m.CurrentPhase,
                m.TotalChannels,
                m.MigratedChannels,
                m.TotalMessages,
                m.MigratedMessages,
                m.TotalMembers,
                m.MigratedMembers,
                m.ErrorMessage,
                m.CreatedAt,
                m.CompletedAt
            ))
            .ToListAsync(cancellationToken);

        return migrations;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/admin/migrations", async (
            ListMigrationsHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new ListMigrationsQuery(), ct);
        })
        .RequireAuthorization(Policies.Admin)
        .Produces<List<MigrationStatusResponse>>(200)
        .WithName("ListMigrations")
        .WithTags("Admin", "Migration");
    }
}
