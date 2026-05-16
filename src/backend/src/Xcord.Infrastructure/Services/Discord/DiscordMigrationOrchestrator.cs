using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services.Discord.Phases;

namespace Xcord.Infrastructure.Services.Discord;

/// <summary>
/// Coordinates the 5-phase Discord server migration pipeline.
/// Each phase is implemented as a separate <see cref="IMigrationPhase"/>
/// and is individually checkpointed so the migration can be resumed after
/// failures.
///
/// Phase classes live in <c>Discord/Phases/</c>. Shared helpers
/// (checkpoint serialization, mention rewriting, message-page migration)
/// live alongside them in <see cref="MigrationPhaseContext"/> and
/// <see cref="MessageChannelMigrator"/>.
/// </summary>
public sealed class DiscordMigrationOrchestrator
{
    private readonly AppDbContext _dbContext;
    private readonly DiscordApiClient _client;
    private readonly ILogger<DiscordMigrationOrchestrator> _logger;
    private readonly MigrationPhaseContext _ctx;

    public DiscordMigrationOrchestrator(
        AppDbContext dbContext,
        DiscordApiClient client,
        SnowflakeIdGenerator snowflakeGenerator,
        IStorageService storageService,
        ILogger<DiscordMigrationOrchestrator> logger)
    {
        _dbContext = dbContext;
        _client = client;
        _logger = logger;
        _ctx = new MigrationPhaseContext(dbContext, client, snowflakeGenerator, storageService, logger);
    }

    public async Task RunAsync(DiscordMigration migration, string botToken, CancellationToken ct)
    {
        _client.SetToken(botToken);

        migration.Status = "Running";
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        var options = JsonSerializer.Deserialize<MigrationOptions>(migration.OptionsJson) ?? new MigrationOptions();

        var messageMigrator = new MessageChannelMigrator(_ctx);

        // Phase ordering is the source of truth for execution order.
        // Threads phase is conditionally skipped via options.ImportThreads.
        var phases = new List<IMigrationPhase>
        {
            new StructurePhase(_ctx),
            new MembersPhase(_ctx),
            new MessagesPhase(_ctx, messageMigrator),
        };

        if (options.ImportThreads)
        {
            phases.Add(new ThreadsPhase(_ctx, messageMigrator));
        }

        phases.Add(new FinalizationPhase(_ctx));

        try
        {
            foreach (var phase in phases)
            {
                await phase.RunAsync(migration, options, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host is shutting down - leave migration in Running state so it can be resumed
            _logger.LogWarning("Migration {MigrationId} interrupted by host shutdown", migration.Id);
            var saved = await TrySaveAsync(migration).ConfigureAwait(false);
            if (!saved)
            {
                // Shutdown-state was not persisted; resume logic must tolerate a Running row that
                // has no recent checkpoint update.
                _logger.LogError("Migration {MigrationId} shutdown state was not persisted", migration.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration {MigrationId} failed in phase {Phase}", migration.Id, migration.CurrentPhase);
            migration.Status = "Failed";
            migration.ErrorMessage = ex.Message;
            var saved = await TrySaveAsync(migration).ConfigureAwait(false);
            if (!saved)
            {
                // Failed-status was not persisted; the migration row may still appear as Running.
                // Surface via re-throw so the host's error pipeline records a hard failure and
                // monitoring can flag the inconsistency for manual investigation.
                throw new InvalidOperationException(
                    $"Migration {migration.Id} failed AND its failure state could not be persisted",
                    ex);
            }
        }
    }

    /// <summary>
    /// Persists the migration entity, returning whether the save succeeded.
    /// Used in shutdown/failure paths where we want to record terminal state but cannot afford
    /// to throw past this point. Callers MUST inspect the return value: if false, the migration
    /// row may be stale (e.g. still "Running") and surface that to the caller / monitoring.
    /// </summary>
    private async Task<bool> TrySaveAsync(DiscordMigration migration)
    {
        try
        {
            await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist terminal state for migration {MigrationId}; DB row may be stale (Status={Status})",
                migration.Id, migration.Status);
            return false;
        }
    }
}
