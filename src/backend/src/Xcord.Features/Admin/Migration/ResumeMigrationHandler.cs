using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services.Discord;

namespace Xcord.Features.Admin.Migration;

public sealed record ResumeMigrationCommand(long MigrationId, string BotToken);

public sealed record ResumeMigrationResponse(
    long MigrationId,
    string Status,
    string? CurrentPhase
);

public sealed class ResumeMigrationHandler(
    AppDbContext dbContext,
    IServiceScopeFactory scopeFactory,
    ILogger<ResumeMigrationHandler> logger)
    : IRequestHandler<ResumeMigrationCommand, Result<ResumeMigrationResponse>>,
      IValidatable<ResumeMigrationCommand>
{
    public Error? Validate(ResumeMigrationCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.BotToken))
            return Error.Validation("VALIDATION_ERROR", "BotToken is required");

        return null;
    }

    public async Task<Result<ResumeMigrationResponse>> Handle(
        ResumeMigrationCommand request, CancellationToken cancellationToken)
    {
        var migration = await dbContext.DiscordMigrations
            .Where(m => m.Id == request.MigrationId && m.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (migration == null)
            return Error.NotFound("MIGRATION_NOT_FOUND", "Migration not found");

        if (migration.Status != "Failed" && migration.Status != "Paused")
        {
            return Error.Conflict("MIGRATION_NOT_RESUMABLE",
                $"Migration cannot be resumed from status '{migration.Status}'. Only Failed or Paused migrations can be resumed.");
        }

        // Check no other active migration running
        var hasActiveMigration = await dbContext.DiscordMigrations
            .AnyAsync(m => m.Id != request.MigrationId
                && m.Status == "Running"
                && m.DeletedAt == null, cancellationToken);

        if (hasActiveMigration)
        {
            return Error.Conflict("MIGRATION_IN_PROGRESS",
                "Another migration is already running. Cancel it before resuming.");
        }

        migration.Status = "Running";
        migration.ErrorMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Resuming Discord migration {MigrationId} from phase {Phase}",
            migration.Id, migration.CurrentPhase);

        var botToken = request.BotToken;
        var migrationId = migration.Id;

        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<DiscordMigrationOrchestrator>();
            var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var migrationToRun = await scopedDb.DiscordMigrations.FindAsync([migrationId]);
            if (migrationToRun == null) return;

            await orchestrator.RunAsync(migrationToRun, botToken, CancellationToken.None);
        }, CancellationToken.None);

        return new ResumeMigrationResponse(
            MigrationId: migration.Id,
            Status: "Running",
            CurrentPhase: migration.CurrentPhase
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/admin/migration/{migrationId:long}/resume", async (
            long migrationId,
            ResumeMigrationBodyRequest body,
            ResumeMigrationHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(
                new ResumeMigrationCommand(migrationId, body.BotToken), ct,
                success => Results.Accepted(null, success));
        })
        .RequireAuthorization(Policies.Admin)
        .Produces<ResumeMigrationResponse>(202)
        .WithName("ResumeMigration")
        .WithTags("Admin", "Migration");
    }
}

public sealed record ResumeMigrationBodyRequest(string BotToken);
