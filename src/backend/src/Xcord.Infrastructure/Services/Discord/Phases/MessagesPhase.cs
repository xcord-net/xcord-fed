using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;

namespace Xcord.Infrastructure.Services.Discord.Phases;

/// <summary>
/// Phase 3 - Messages: paginate every previously-imported channel and
/// migrate its full message history via <see cref="MessageChannelMigrator"/>.
/// </summary>
public sealed class MessagesPhase : IMigrationPhase
{
    private readonly MigrationPhaseContext _ctx;
    private readonly MessageChannelMigrator _messageMigrator;

    public MessagesPhase(MigrationPhaseContext ctx, MessageChannelMigrator messageMigrator)
    {
        _ctx = ctx;
        _messageMigrator = messageMigrator;
    }

    public string PhaseName => "Messages";

    public async Task RunAsync(DiscordMigration migration, MigrationOptions options, CancellationToken ct)
    {
        if (MigrationPhaseContext.IsPhaseDone(migration, PhaseName))
            return;

        var dbContext = _ctx.DbContext;
        var logger = _ctx.Logger;

        migration.CurrentPhase = PhaseName;
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        if (await _ctx.IsStoppedAsync(migration.Id, ct)) return;

        var channelMappings = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "Channel")
            .ToListAsync(ct);

        var userLookup = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "User")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        var checkpoint = MigrationPhaseContext.LoadCheckpoint(migration);

        long totalMessages = migration.MigratedMessages;

        // Build channel->conversation lookup
        var channelToConversation = await dbContext.Channels
            .Where(c => c.ServerId == migration.ServerId && c.DeletedAt == null)
            .ToDictionaryAsync(c => c.Id, c => c.ConversationId, ct);

        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        foreach (var channelMap in channelMappings)
        {
            ct.ThrowIfCancellationRequested();
            if (await _ctx.IsStoppedAsync(migration.Id, ct)) return;

            var xcordChannelId = channelMap.XcordId;
            if (!channelToConversation.TryGetValue(xcordChannelId, out var conversationId))
            {
                logger.LogWarning(
                    "Channel {ChannelId} has no conversation, skipping", xcordChannelId);
                continue;
            }

            // If channel is already done, skip
            if (checkpoint.DoneChannels.Contains(channelMap.DiscordId))
                continue;

            totalMessages = await _messageMigrator.MigrateAsync(
                migration, channelMap.DiscordId, conversationId,
                userLookup, options, totalMessages, checkpoint, ct);

            checkpoint.DoneChannels.Add(channelMap.DiscordId);
            migration.MigratedMessages = totalMessages;
            migration.CheckpointJson = JsonSerializer.Serialize(checkpoint);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
        }

        dbContext.ChangeTracker.AutoDetectChangesEnabled = true;

        migration.MigratedMessages = totalMessages;
        MigrationPhaseContext.SetPhaseDone(migration, PhaseName);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Migration {MigrationId} Phase 3 complete: {MessageCount} messages",
            migration.Id, totalMessages);
    }
}
