using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services.Discord;

/// <summary>
/// Dependencies + shared helpers passed to every <see cref="IMigrationPhase"/>.
/// Holds the singleton-per-migration services and shared checkpoint utilities
/// that were originally private members of the monolithic orchestrator.
/// </summary>
public sealed class MigrationPhaseContext
{
    public const int BatchSize = 500;

    public MigrationPhaseContext(
        AppDbContext dbContext,
        DiscordApiClient client,
        SnowflakeIdGenerator snowflakeGenerator,
        IStorageService storageService,
        ILogger logger)
    {
        DbContext = dbContext;
        Client = client;
        SnowflakeGenerator = snowflakeGenerator;
        StorageService = storageService;
        Logger = logger;
    }

    public AppDbContext DbContext { get; }
    public DiscordApiClient Client { get; }
    public SnowflakeIdGenerator SnowflakeGenerator { get; }
    public IStorageService StorageService { get; }
    public ILogger Logger { get; }

    // -------------------------------------------------------------------------
    // Checkpoint helpers shared by every phase
    // -------------------------------------------------------------------------

    public static bool IsPhaseDone(DiscordMigration migration, string phase)
    {
        if (migration.CheckpointJson == null) return false;
        var checkpoint = JsonSerializer.Deserialize<MigrationCheckpoint>(migration.CheckpointJson);
        return checkpoint?.DonePhases.Contains(phase) ?? false;
    }

    public static void SetPhaseDone(DiscordMigration migration, string phase)
    {
        var checkpoint = LoadCheckpoint(migration);
        checkpoint.DonePhases.Add(phase);
        migration.CheckpointJson = JsonSerializer.Serialize(checkpoint);
    }

    public static MigrationCheckpoint LoadCheckpoint(DiscordMigration migration)
    {
        return migration.CheckpointJson != null
            ? JsonSerializer.Deserialize<MigrationCheckpoint>(migration.CheckpointJson) ?? new MigrationCheckpoint()
            : new MigrationCheckpoint();
    }

    public async Task<bool> IsStoppedAsync(long migrationId, CancellationToken ct)
    {
        // Reload from DB to detect cancel
        var current = await DbContext.DiscordMigrations
            .AsNoTracking()
            .Where(m => m.Id == migrationId)
            .Select(m => m.Status)
            .FirstOrDefaultAsync(ct);
        return current != "Running";
    }

    // -------------------------------------------------------------------------
    // Mention rewriting / username sanitization shared by message-producing phases
    // -------------------------------------------------------------------------

    public static string RewriteMentions(
        string content,
        Dictionary<string, long> userLookup,
        Dictionary<string, long> groupLookup,
        Dictionary<string, long> channelLookup)
    {
        if (string.IsNullOrEmpty(content)) return content;

        // Replace user mentions: <@discordId> or <@!discordId>
        content = Regex.Replace(content, @"<@!?(\d+)>", match =>
        {
            var discordId = match.Groups[1].Value;
            return userLookup.TryGetValue(discordId, out var xcordId)
                ? $"<@{xcordId}>" : match.Value;
        });

        // Replace role mentions: <@&discordRoleId>
        content = Regex.Replace(content, @"<@&(\d+)>", match =>
        {
            var discordId = match.Groups[1].Value;
            return groupLookup.TryGetValue(discordId, out var xcordId)
                ? $"<@&{xcordId}>" : match.Value;
        });

        // Replace channel mentions: <#discordChannelId>
        content = Regex.Replace(content, @"<#(\d+)>", match =>
        {
            var discordId = match.Groups[1].Value;
            return channelLookup.TryGetValue(discordId, out var xcordId)
                ? $"<#{xcordId}>" : match.Value;
        });

        return content;
    }

    public static string SanitizeUsername(string username)
    {
        // Ensure username is valid: lowercase, alphanumeric + underscore, max 32 chars
        var sanitized = Regex.Replace(username.ToLowerInvariant(), @"[^a-z0-9_]", "_");
        if (sanitized.Length > 32) sanitized = sanitized[..32];
        if (string.IsNullOrEmpty(sanitized)) sanitized = "user";
        return sanitized;
    }
}

/// <summary>
/// Contract implemented by every migration phase.
/// Phases are executed in order by <see cref="DiscordMigrationOrchestrator"/>.
/// Each phase must be idempotent — it should consult the migration's
/// checkpoint via <see cref="MigrationPhaseContext.IsPhaseDone"/> and skip
/// itself if it has already run.
/// </summary>
public interface IMigrationPhase
{
    /// <summary>Phase name used in checkpoint state and logs.</summary>
    string PhaseName { get; }

    Task RunAsync(DiscordMigration migration, MigrationOptions options, CancellationToken ct);
}
