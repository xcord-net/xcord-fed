using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Exceptions;
using Xcord.Entities;
using XcordGroup = Xcord.Entities.Group;

namespace Xcord.Infrastructure.Services.Discord.Phases;

/// <summary>
/// Phase 1 - Structure: server, groups (from Discord roles), categories,
/// channels, and custom emoji. Idempotent via the migration checkpoint.
/// </summary>
public sealed class StructurePhase : IMigrationPhase
{
    private readonly MigrationPhaseContext _ctx;

    public StructurePhase(MigrationPhaseContext ctx)
    {
        _ctx = ctx;
    }

    public string PhaseName => "Structure";

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

        var now = DateTimeOffset.UtcNow;
        var guildId = migration.DiscordGuildId;

        // Fetch guild info
        var guild = await client.GetGuildAsync(guildId, ct).ConfigureAwait(false);
        var guildName = guild.GetProperty("name").GetString() ?? "Imported Server";
        var systemChannelDiscordId = guild.TryGetProperty("system_channel_id", out var sysChEl)
            ? sysChEl.ValueKind == JsonValueKind.Null ? null : sysChEl.GetString()
            : null;

        // Update server entity with guild info
        var server = await dbContext.Servers.FindAsync([migration.ServerId], ct)
            ?? throw new InvalidMigrationStateException(
                $"Server {migration.ServerId} not found for migration {migration.Id}",
                PhaseName);

        server.Name = guildName;

        if (guild.TryGetProperty("description", out var descEl) && descEl.ValueKind != JsonValueKind.Null)
            server.Description = descEl.GetString();

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        // Phase 1a - Roles -> Groups
        var roles = await client.GetGuildRolesAsync(guildId, ct).ConfigureAwait(false);
        var idMappings = new List<DiscordIdMapping>();
        int position = 0;

        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        foreach (var role in roles.EnumerateArray())
        {
            var discordRoleId = role.GetProperty("id").GetString()!;
            var roleName = role.GetProperty("name").GetString() ?? "Unknown Role";
            var discordPerms = long.Parse(role.GetProperty("permissions").GetString() ?? "0");
            var colorInt = role.TryGetProperty("color", out var colorEl) ? colorEl.GetInt32() : 0;
            var colorHex = colorInt != 0 ? $"#{colorInt:X6}" : null;
            var isEveryone = discordRoleId == guildId;

            var groupId = snowflakeGenerator.NextId();
            var group = new XcordGroup
            {
                Id = groupId,
                ServerId = migration.ServerId,
                Name = roleName,
                Color = colorHex,
                Roles = DiscordPermissionMapper.MapPermissions(discordPerms),
                Position = position++,
                IsEveryone = isEveryone,
                CreatedAt = now
            };

            dbContext.Groups.Add(group);
            idMappings.Add(new DiscordIdMapping
            {
                Id = snowflakeGenerator.NextId(),
                MigrationId = migration.Id,
                DiscordId = discordRoleId,
                XcordId = groupId,
                EntityType = "Group"
            });

            if (idMappings.Count >= MigrationPhaseContext.BatchSize)
            {
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                dbContext.ChangeTracker.Clear();
            }
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();

        // Build an in-memory lookup for the rest of Phase 1
        var groupMapping = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "Group")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        // Phase 1b - Channels -> Categories + Channels + Conversations
        var channels = await client.GetGuildChannelsAsync(guildId, ct).ConfigureAwait(false);
        var categoryMappings = new List<DiscordIdMapping>();
        var channelMappings = new List<DiscordIdMapping>();
        var channelCount = 0;
        long? systemChannelXcordId = null;

        // First pass: create categories (type 4)
        foreach (var ch in channels.EnumerateArray())
        {
            var chType = ch.GetProperty("type").GetInt32();
            if (chType != 4) continue;

            var discordCatId = ch.GetProperty("id").GetString()!;
            var catName = ch.GetProperty("name").GetString() ?? "Category";
            var catPosition = ch.TryGetProperty("position", out var posEl) ? posEl.GetInt32() : 0;

            var catId = snowflakeGenerator.NextId();
            var category = new Category
            {
                Id = catId,
                ServerId = migration.ServerId,
                Name = catName,
                Position = catPosition,
                CreatedAt = now
            };

            dbContext.Categories.Add(category);
            categoryMappings.Add(new DiscordIdMapping
            {
                Id = snowflakeGenerator.NextId(),
                MigrationId = migration.Id,
                DiscordId = discordCatId,
                XcordId = catId,
                EntityType = "Category"
            });
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();

        // Build category lookup
        var categoryMapping = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "Category")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        // Save category ID mappings
        dbContext.DiscordIdMappings.AddRange(categoryMappings);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();

        // Second pass: create channels (types 0, 2, 5, 15)
        var overrideBatch = new List<ChannelPermissionOverride>();

        foreach (var ch in channels.EnumerateArray())
        {
            var chType = ch.GetProperty("type").GetInt32();
            if (chType != 0 && chType != 2 && chType != 5 && chType != 15) continue;

            var discordChannelId = ch.GetProperty("id").GetString()!;
            var chName = ch.GetProperty("name").GetString() ?? "channel";
            var chPosition = ch.TryGetProperty("position", out var posEl2) ? posEl2.GetInt32() : 0;
            var chTopic = ch.TryGetProperty("topic", out var topicEl) && topicEl.ValueKind != JsonValueKind.Null
                ? topicEl.GetString() : null;
            var isNsfw = ch.TryGetProperty("nsfw", out var nsfwEl) && nsfwEl.GetBoolean();
            var slowMode = ch.TryGetProperty("rate_limit_per_user", out var slowEl) ? (int?)slowEl.GetInt32() : null;
            if (slowMode == 0) slowMode = null;

            long? categoryId = null;
            if (ch.TryGetProperty("parent_id", out var parentEl) && parentEl.ValueKind != JsonValueKind.Null)
            {
                var parentDiscordId = parentEl.GetString()!;
                if (categoryMapping.TryGetValue(parentDiscordId, out var catXcordId))
                    categoryId = catXcordId;
            }

            var channelType = chType switch
            {
                0 => ChannelType.Text,
                2 => ChannelType.Voice,
                5 => ChannelType.Announcement,
                15 => ChannelType.Forum,
                _ => ChannelType.Text
            };

            var channelCapabilities = chType switch
            {
                0 => ChannelCapability.Chat,
                2 => ChannelCapability.Voice | ChannelCapability.Video,
                5 => ChannelCapability.Announcement | ChannelCapability.Chat,
                15 => ChannelCapability.Forum | ChannelCapability.Chat,
                _ => ChannelCapability.Chat
            };

            var convId = snowflakeGenerator.NextId();
            var conversation = new Conversation
            {
                Id = convId,
                Type = ConversationType.Channel
            };
            dbContext.Conversations.Add(conversation);

            var channelId = snowflakeGenerator.NextId();
            var channel = new Channel
            {
                Id = channelId,
                ConversationId = convId,
                ServerId = migration.ServerId,
                CategoryId = categoryId,
                Name = chName,
                Topic = chTopic,
                Type = channelType,
                Capabilities = channelCapabilities,
                Position = chPosition,
                SlowModeSeconds = slowMode,
                IsNsfw = isNsfw,
                CreatedAt = now
            };
            dbContext.Channels.Add(channel);

            channelMappings.Add(new DiscordIdMapping
            {
                Id = snowflakeGenerator.NextId(),
                MigrationId = migration.Id,
                DiscordId = discordChannelId,
                XcordId = channelId,
                EntityType = "Channel"
            });

            if (discordChannelId == systemChannelDiscordId)
                systemChannelXcordId = channelId;

            channelCount++;

            // Permission overwrites
            if (ch.TryGetProperty("permission_overwrites", out var overwrites))
            {
                foreach (var overwrite in overwrites.EnumerateArray())
                {
                    var targetDiscordId = overwrite.GetProperty("id").GetString()!;
                    var targetTypeInt = overwrite.GetProperty("type").GetInt32();
                    var allowBits = long.Parse(overwrite.GetProperty("allow").GetString() ?? "0");
                    var denyBits = long.Parse(overwrite.GetProperty("deny").GetString() ?? "0");

                    // type 0 = role, type 1 = member
                    // We'll resolve the TargetId after saving groups/users - store temporarily with discord ID encoded
                    // For now: skip member overwrites as users aren't imported yet in Phase 1
                    if (targetTypeInt == 0 && groupMapping.TryGetValue(targetDiscordId, out var groupXcordId))
                    {
                        overrideBatch.Add(new ChannelPermissionOverride
                        {
                            Id = snowflakeGenerator.NextId(),
                            ChannelId = channelId,
                            TargetType = OverrideTargetType.Group,
                            TargetId = groupXcordId,
                            Allow = DiscordPermissionMapper.MapPermissions(allowBits),
                            Deny = DiscordPermissionMapper.MapPermissions(denyBits)
                        });
                    }
                }
            }

            if (channelCount % MigrationPhaseContext.BatchSize == 0)
            {
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                dbContext.ChangeTracker.Clear();
            }
        }

        if (overrideBatch.Count > 0)
            dbContext.ChannelPermissionOverrides.AddRange(overrideBatch);

        dbContext.DiscordIdMappings.AddRange(channelMappings);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();

        // Phase 1c - Custom Emoji
        if (options.ImportEmoji)
        {
            await MigrateEmojisAsync(migration, guildId, now, ct).ConfigureAwait(false);
        }

        // Two-phase save: set SystemChannelId after channels are persisted
        if (systemChannelXcordId.HasValue)
        {
            var serverEntity = await dbContext.Servers.FindAsync([migration.ServerId], ct).ConfigureAwait(false);
            if (serverEntity != null)
            {
                serverEntity.SystemChannelId = systemChannelXcordId;
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }

        dbContext.ChangeTracker.AutoDetectChangesEnabled = true;

        migration.TotalChannels = channelCount;
        migration.MigratedChannels = channelCount;
        MigrationPhaseContext.SetPhaseDone(migration, PhaseName);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Migration {MigrationId} Phase 1 complete: {ChannelCount} channels, {GroupCount} groups",
            migration.Id, channelCount, groupMapping.Count);
    }

    private async Task MigrateEmojisAsync(
        DiscordMigration migration, string guildId, DateTimeOffset now, CancellationToken ct)
    {
        var dbContext = _ctx.DbContext;
        var client = _ctx.Client;
        var snowflakeGenerator = _ctx.SnowflakeGenerator;
        var storageService = _ctx.StorageService;
        var logger = _ctx.Logger;

        var emojis = await client.GetGuildEmojisAsync(guildId, ct).ConfigureAwait(false);

        // We need a creator user ID - use server owner
        var server = await dbContext.Servers.FindAsync([migration.ServerId], ct).ConfigureAwait(false);
        if (server == null) return;

        var emojiMappings = new List<DiscordIdMapping>();

        foreach (var emoji in emojis.EnumerateArray())
        {
            if (!emoji.TryGetProperty("id", out var idEl) || idEl.ValueKind == JsonValueKind.Null) continue;
            var discordEmojiId = idEl.GetString()!;
            var emojiName = emoji.GetProperty("name").GetString() ?? "emoji";
            var isAnimated = emoji.TryGetProperty("animated", out var animEl) && animEl.GetBoolean();

            var ext = isAnimated ? "gif" : "png";
            var cdnUrl = $"https://cdn.discordapp.com/emojis/{discordEmojiId}.{ext}";

            try
            {
                using var stream = await client.DownloadAsync(cdnUrl, ct).ConfigureAwait(false);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
                var bytes = ms.ToArray();

                var s3Key = $"emojis/{migration.ServerId}/{discordEmojiId}.{ext}";
                var contentType = isAnimated ? "image/gif" : "image/png";
                await storageService.UploadAsync(s3Key, bytes, contentType).ConfigureAwait(false);

                var emojiId = snowflakeGenerator.NextId();
                var customEmoji = new CustomEmoji
                {
                    Id = emojiId,
                    ServerId = migration.ServerId,
                    Name = emojiName,
                    ImageUrl = s3Key, // Store S3 key; frontend resolves via CDN
                    S3Key = s3Key,
                    IsAnimated = isAnimated,
                    CreatorId = server.OwnerId,
                    CreatedAt = now
                };
                dbContext.CustomEmojis.Add(customEmoji);

                emojiMappings.Add(new DiscordIdMapping
                {
                    Id = snowflakeGenerator.NextId(),
                    MigrationId = migration.Id,
                    DiscordId = discordEmojiId,
                    XcordId = emojiId,
                    EntityType = "Emoji"
                });

                if (emojiMappings.Count >= MigrationPhaseContext.BatchSize)
                {
                    await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                    dbContext.ChangeTracker.Clear();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to migrate emoji {EmojiId} ({EmojiName})", discordEmojiId, emojiName);
            }
        }

        if (emojiMappings.Count > 0)
        {
            dbContext.DiscordIdMappings.AddRange(emojiMappings);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
        }
    }
}
