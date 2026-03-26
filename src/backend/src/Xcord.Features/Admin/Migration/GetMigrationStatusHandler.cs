using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Admin.Migration;

public sealed record GetMigrationStatusQuery(long MigrationId);

public sealed record MigrationStatusResponse(
    long Id,
    string DiscordGuildId,
    long ServerId,
    string Status,
    string? CurrentPhase,
    int TotalChannels,
    int MigratedChannels,
    long TotalMessages,
    long MigratedMessages,
    int TotalMembers,
    int MigratedMembers,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt
);

public sealed class GetMigrationStatusHandler(AppDbContext dbContext)
    : IRequestHandler<GetMigrationStatusQuery, Result<MigrationStatusResponse>>
{
    public async Task<Result<MigrationStatusResponse>> Handle(
        GetMigrationStatusQuery request, CancellationToken cancellationToken)
    {
        var migration = await dbContext.DiscordMigrations
            .AsNoTracking()
            .Where(m => m.Id == request.MigrationId && m.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (migration == null)
            return Error.NotFound("MIGRATION_NOT_FOUND", "Migration not found");

        return new MigrationStatusResponse(
            Id: migration.Id,
            DiscordGuildId: migration.DiscordGuildId,
            ServerId: migration.ServerId,
            Status: migration.Status,
            CurrentPhase: migration.CurrentPhase,
            TotalChannels: migration.TotalChannels,
            MigratedChannels: migration.MigratedChannels,
            TotalMessages: migration.TotalMessages,
            MigratedMessages: migration.MigratedMessages,
            TotalMembers: migration.TotalMembers,
            MigratedMembers: migration.MigratedMembers,
            ErrorMessage: migration.ErrorMessage,
            CreatedAt: migration.CreatedAt,
            CompletedAt: migration.CompletedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/admin/migration/{migrationId:long}", async (
            long migrationId,
            GetMigrationStatusHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(
                new GetMigrationStatusQuery(migrationId), ct);
        })
        .RequireAuthorization(Policies.Admin)
        .Produces<MigrationStatusResponse>(200)
        .WithName("GetMigrationStatus")
        .WithTags("Admin", "Migration");
    }
}
