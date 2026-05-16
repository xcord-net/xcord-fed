using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;

namespace Xcord.Infrastructure.Services.Discord.Phases;

/// <summary>
/// Phase 5 - Finalization: rolls up final member counts on the server row
/// and marks the migration as Completed.
/// </summary>
public sealed class FinalizationPhase : IMigrationPhase
{
    private readonly MigrationPhaseContext _ctx;

    public FinalizationPhase(MigrationPhaseContext ctx)
    {
        _ctx = ctx;
    }

    public string PhaseName => "Finalization";

    public async Task RunAsync(DiscordMigration migration, MigrationOptions options, CancellationToken ct)
    {
        var dbContext = _ctx.DbContext;
        var logger = _ctx.Logger;

        migration.CurrentPhase = PhaseName;
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        var memberCount = await dbContext.ServerMembers
            .CountAsync(m => m.ServerId == migration.ServerId && m.DeletedAt == null, ct);

        var server = await dbContext.Servers.FindAsync([migration.ServerId], ct).ConfigureAwait(false);
        if (server != null)
        {
            server.MemberCount = memberCount;
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        migration.Status = "Completed";
        migration.CompletedAt = DateTimeOffset.UtcNow;
        migration.TotalMembers = memberCount;
        migration.MigratedMembers = memberCount;
        MigrationPhaseContext.SetPhaseDone(migration, PhaseName);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Migration {MigrationId} completed successfully for server {ServerId}",
            migration.Id, migration.ServerId);
    }
}
