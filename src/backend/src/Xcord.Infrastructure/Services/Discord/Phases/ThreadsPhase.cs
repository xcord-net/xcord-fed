using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;

namespace Xcord.Infrastructure.Services.Discord.Phases;

/// <summary>
/// Phase 4 - Threads: import every active + archived thread for the guild,
/// create the corresponding Xcord Thread + Conversation rows, and migrate
/// thread messages via <see cref="MessageChannelMigrator"/>.
///
/// Skipped when <see cref="MigrationOptions.ImportThreads"/> is false.
/// </summary>
public sealed class ThreadsPhase : IMigrationPhase
{
    private readonly MigrationPhaseContext _ctx;
    private readonly MessageChannelMigrator _messageMigrator;

    public ThreadsPhase(MigrationPhaseContext ctx, MessageChannelMigrator messageMigrator)
    {
        _ctx = ctx;
        _messageMigrator = messageMigrator;
    }

    public string PhaseName => "Threads";

    public async Task RunAsync(DiscordMigration migration, MigrationOptions options, CancellationToken ct)
    {
        if (MigrationPhaseContext.IsPhaseDone(migration, PhaseName))
            return;

        var dbContext = _ctx.DbContext;
        var client = _ctx.Client;
        var snowflakeGenerator = _ctx.SnowflakeGenerator;
        var logger = _ctx.Logger;

        migration.CurrentPhase = PhaseName;
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        if (await _ctx.IsStoppedAsync(migration.Id, ct)) return;

        var userLookup = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "User")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        var channelMappings = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "Channel")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        var checkpoint = MigrationPhaseContext.LoadCheckpoint(migration);

        var now = DateTimeOffset.UtcNow;

        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        // Fetch active threads
        var activeThreadsResult = await client.GetActiveThreadsAsync(migration.DiscordGuildId, ct).ConfigureAwait(false);
        var allThreads = new List<System.Text.Json.JsonElement>();

        if (activeThreadsResult.TryGetProperty("threads", out var activeThreadsEl))
            allThreads.AddRange(activeThreadsEl.EnumerateArray());

        // Fetch archived threads per channel
        foreach (var channelDiscordId in channelMappings.Keys)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var archivedResult = await client.GetArchivedThreadsAsync(channelDiscordId, ct).ConfigureAwait(false);
                if (archivedResult.TryGetProperty("threads", out var archivedEl))
                    allThreads.AddRange(archivedEl.EnumerateArray());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch archived threads for channel {ChannelId}", channelDiscordId);
            }
        }

        long totalMessages = migration.MigratedMessages;

        foreach (var thread in allThreads)
        {
            ct.ThrowIfCancellationRequested();
            if (await _ctx.IsStoppedAsync(migration.Id, ct)) break;

            var discordThreadId = thread.GetProperty("id").GetString()!;
            if (checkpoint.DoneChannels.Contains($"thread:{discordThreadId}")) continue;

            var threadName = thread.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString() ?? "Thread" : "Thread";

            var discordParentId = thread.TryGetProperty("parent_id", out var pidEl)
                && pidEl.ValueKind != JsonValueKind.Null
                ? pidEl.GetString() : null;

            long? xcordChannelId = discordParentId != null && channelMappings.TryGetValue(discordParentId, out var cid)
                ? cid : null;

            if (!xcordChannelId.HasValue)
            {
                logger.LogWarning("Thread {ThreadId} has no mapped parent channel, skipping", discordThreadId);
                continue;
            }

            // Resolve starter message
            long? parentMessageId = null;
            if (thread.TryGetProperty("id", out _))
            {
                var existingMapping = await dbContext.DiscordIdMappings
                    .Where(m => m.MigrationId == migration.Id
                        && m.EntityType == "Message"
                        && m.DiscordId == discordThreadId)
                    .FirstOrDefaultAsync(ct);
                if (existingMapping != null)
                    parentMessageId = existingMapping.XcordId;
            }

            var isArchived = thread.TryGetProperty("thread_metadata", out var metaEl)
                && metaEl.TryGetProperty("archived", out var arcEl)
                && arcEl.GetBoolean();

            var isLocked = thread.TryGetProperty("thread_metadata", out var meta2El)
                && meta2El.TryGetProperty("locked", out var lockEl)
                && lockEl.GetBoolean();

            var convId = snowflakeGenerator.NextId();
            dbContext.Conversations.Add(new Conversation
            {
                Id = convId,
                Type = ConversationType.Thread
            });

            var threadId = snowflakeGenerator.NextId();
            var threadEntity = new Xcord.Entities.Thread
            {
                Id = threadId,
                ConversationId = convId,
                ChannelId = xcordChannelId.Value,
                ParentMessageId = parentMessageId,
                Title = threadName[..Math.Min(threadName.Length, 100)],
                IsArchived = isArchived,
                IsLocked = isLocked,
                AutoArchiveDurationMinutes = 1440,
                LastActivityAt = now,
                CreatedAt = now
            };
            dbContext.Threads.Add(threadEntity);

            // Thread members
            try
            {
                var threadMembers = await client.GetThreadMembersAsync(discordThreadId, ct).ConfigureAwait(false);
                var threadMemberBatch = new List<ThreadMember>();

                foreach (var tm in threadMembers.EnumerateArray())
                {
                    var discordUserId = tm.TryGetProperty("user_id", out var uidEl)
                        ? uidEl.GetString() : null;
                    if (discordUserId == null) continue;
                    if (!userLookup.TryGetValue(discordUserId, out var xcordUserId)) continue;

                    var joinedAtStr = tm.TryGetProperty("join_timestamp", out var jtEl) ? jtEl.GetString() : null;
                    var joinedAt = joinedAtStr != null && DateTimeOffset.TryParse(joinedAtStr, out var jd) ? jd : now;

                    threadMemberBatch.Add(new ThreadMember
                    {
                        UserId = xcordUserId,
                        ThreadId = threadId,
                        JoinedAt = joinedAt
                    });
                }

                if (threadMemberBatch.Count > 0)
                    dbContext.ThreadMembers.AddRange(threadMemberBatch);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch thread members for thread {ThreadId}", discordThreadId);
            }

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();

            // Migrate thread messages
            totalMessages = await _messageMigrator.MigrateAsync(
                migration, discordThreadId, convId,
                userLookup, options, totalMessages, checkpoint, ct);

            checkpoint.DoneChannels.Add($"thread:{discordThreadId}");
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
            "Migration {MigrationId} Phase 4 complete: {ThreadCount} threads processed",
            migration.Id, allThreads.Count);
    }
}
