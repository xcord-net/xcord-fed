using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Admin.Migration;

public sealed record CancelMigrationCommand(long MigrationId);

public sealed class CancelMigrationHandler(AppDbContext dbContext)
    : IRequestHandler<CancelMigrationCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        CancelMigrationCommand request, CancellationToken cancellationToken)
    {
        var migration = await dbContext.DiscordMigrations
            .Where(m => m.Id == request.MigrationId && m.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (migration == null)
            return Error.NotFound("MIGRATION_NOT_FOUND", "Migration not found");

        if (migration.Status == "Completed")
            return Error.Conflict("MIGRATION_ALREADY_COMPLETED",
                "Cannot cancel a completed migration");

        if (migration.Status == "Failed")
            return Error.Conflict("MIGRATION_ALREADY_FAILED",
                "Migration is already in a failed state");

        migration.Status = "Failed";
        migration.ErrorMessage = "Cancelled by admin";

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/admin/migration/{migrationId:long}/cancel", async (
            long migrationId,
            CancelMigrationHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(
                new CancelMigrationCommand(migrationId), ct,
                _ => Results.NoContent());
        })
        .RequireAuthorization(Policies.Admin)
        .Produces(204)
        .WithName("CancelMigration")
        .WithTags("Admin", "Migration");
    }
}
