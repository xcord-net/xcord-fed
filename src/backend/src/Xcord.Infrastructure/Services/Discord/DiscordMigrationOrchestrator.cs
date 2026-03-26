using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using XcordGroup = Xcord.Entities.Group;

namespace Xcord.Infrastructure.Services.Discord;

/// <summary>
/// Runs the 5-phase Discord server migration pipeline.
/// Each phase is checkpointed so migration can be resumed after failures.
/// </summary>
public sealed class DiscordMigrationOrchestrator(
    AppDbContext dbContext,
    DiscordApiClient client,
    SnowflakeIdGenerator snowflakeGenerator,
    IStorageService storageService,
    ILogger<DiscordMigrationOrchestrator> logger)
{
    private const int BatchSize = 500;

    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    public async Task RunAsync(DiscordMigration migration, string botToken, CancellationToken ct)
    {
        client.SetToken(botToken);

        migration.Status = "Running";
        await dbContext.SaveChangesAsync(ct);

        var options = JsonSerializer.Deserialize<MigrationOptions>(migration.OptionsJson) ?? new MigrationOptions();

        try
        {
            await RunPhase1StructureAsync(migration, options, ct);
            await RunPhase2MembersAsync(migration, options, ct);
            await RunPhase3MessagesAsync(migration, options, ct);

            if (options.ImportThreads)
                await RunPhase4ThreadsAsync(migration, options, ct);

            await RunPhase5FinalizationAsync(migration, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host is shutting down - leave migration in Running state so it can be resumed
            logger.LogWarning("Migration {MigrationId} interrupted by host shutdown", migration.Id);
            await TrySaveAsync(migration, ct: CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration {MigrationId} failed in phase {Phase}", migration.Id, migration.CurrentPhase);
            migration.Status = "Failed";
            migration.ErrorMessage = ex.Message;
            await TrySaveAsync(migration, ct: CancellationToken.None);
        }
    }

    // -------------------------------------------------------------------------
    // Phase 1 - Structure (server, groups, categories, channels, emoji)
    // -------------------------------------------------------------------------

    private async Task RunPhase1StructureAsync(DiscordMigration migration, MigrationOptions options, CancellationToken ct)
    {
        if (IsPhaseDone(migration, "Structure"))
            return;

        migration.CurrentPhase = "Structure";
        await dbContext.SaveChangesAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var guildId = migration.DiscordGuildId;

        // Fetch guild info
        var guild = await client.GetGuildAsync(guildId, ct);
        var guildName = guild.GetProperty("name").GetString() ?? "Imported Server";
        var systemChannelDiscordId = guild.TryGetProperty("system_channel_id", out var sysChEl)
            ? sysChEl.ValueKind == JsonValueKind.Null ? null : sysChEl.GetString()
            : null;

        // Update server entity with guild info
        var server = await dbContext.Servers.FindAsync([migration.ServerId], ct)
            ?? throw new InvalidOperationException($"Server {migration.ServerId} not found");

        server.Name = guildName;

        if (guild.TryGetProperty("description", out var descEl) && descEl.ValueKind != JsonValueKind.Null)
            server.Description = descEl.GetString();

        await dbContext.SaveChangesAsync(ct);

        // Phase 1a - Roles -> Groups
        var roles = await client.GetGuildRolesAsync(guildId, ct);
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

            if (idMappings.Count >= BatchSize)
            {
                await dbContext.SaveChangesAsync(ct);
                dbContext.ChangeTracker.Clear();
            }
        }

        await dbContext.SaveChangesAsync(ct);
        dbContext.ChangeTracker.Clear();

        // Build an in-memory lookup for the rest of Phase 1
        var groupMapping = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "Group")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        // Phase 1b - Channels -> Categories + Channels + Conversations
        var channels = await client.GetGuildChannelsAsync(guildId, ct);
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

        await dbContext.SaveChangesAsync(ct);
        dbContext.ChangeTracker.Clear();

        // Build category lookup
        var categoryMapping = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "Category")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        // Save category ID mappings
        dbContext.DiscordIdMappings.AddRange(categoryMappings);
        await dbContext.SaveChangesAsync(ct);
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

            if (channelCount % BatchSize == 0)
            {
                await dbContext.SaveChangesAsync(ct);
                dbContext.ChangeTracker.Clear();
            }
        }

        if (overrideBatch.Count > 0)
            dbContext.ChannelPermissionOverrides.AddRange(overrideBatch);

        dbContext.DiscordIdMappings.AddRange(channelMappings);
        await dbContext.SaveChangesAsync(ct);
        dbContext.ChangeTracker.Clear();

        // Phase 1c - Custom Emoji
        if (options.ImportEmoji)
        {
            await MigrateEmojisAsync(migration, guildId, options, now, ct);
        }

        // Two-phase save: set SystemChannelId after channels are persisted
        if (systemChannelXcordId.HasValue)
        {
            var serverEntity = await dbContext.Servers.FindAsync([migration.ServerId], ct);
            if (serverEntity != null)
            {
                serverEntity.SystemChannelId = systemChannelXcordId;
                await dbContext.SaveChangesAsync(ct);
            }
        }

        dbContext.ChangeTracker.AutoDetectChangesEnabled = true;

        migration.TotalChannels = channelCount;
        migration.MigratedChannels = channelCount;
        SetPhaseDone(migration, "Structure");
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Migration {MigrationId} Phase 1 complete: {ChannelCount} channels, {GroupCount} groups",
            migration.Id, channelCount, groupMapping.Count);
    }

    private async Task MigrateEmojisAsync(
        DiscordMigration migration, string guildId, MigrationOptions options, DateTimeOffset now, CancellationToken ct)
    {
        var emojis = await client.GetGuildEmojisAsync(guildId, ct);

        // We need a creator user ID - use server owner
        var server = await dbContext.Servers.FindAsync([migration.ServerId], ct);
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
                using var stream = await client.DownloadAsync(cdnUrl, ct);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                var bytes = ms.ToArray();

                var s3Key = $"emojis/{migration.ServerId}/{discordEmojiId}.{ext}";
                var contentType = isAnimated ? "image/gif" : "image/png";
                await storageService.UploadAsync(s3Key, bytes, contentType);

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

                if (emojiMappings.Count >= BatchSize)
                {
                    await dbContext.SaveChangesAsync(ct);
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
            await dbContext.SaveChangesAsync(ct);
            dbContext.ChangeTracker.Clear();
        }
    }

    // -------------------------------------------------------------------------
    // Phase 2 - Members
    // -------------------------------------------------------------------------

    private async Task RunPhase2MembersAsync(DiscordMigration migration, MigrationOptions options, CancellationToken ct)
    {
        if (IsPhaseDone(migration, "Members"))
            return;

        migration.CurrentPhase = "Members";
        await dbContext.SaveChangesAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var guildId = migration.DiscordGuildId;

        // Check if migration was cancelled
        if (await IsStoppedAsync(migration.Id, ct)) return;

        var groupLookup = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "Group")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        // Determine last paginated user ID from checkpoint
        var checkpoint = migration.CheckpointJson != null
            ? JsonSerializer.Deserialize<MigrationCheckpoint>(migration.CheckpointJson) ?? new MigrationCheckpoint()
            : new MigrationCheckpoint();

        string? afterMemberId = checkpoint.LastMemberDiscordId;
        int totalMigrated = migration.MigratedMembers;

        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (await IsStoppedAsync(migration.Id, ct)) break;

            var membersPage = await client.GetGuildMembersAsync(guildId, limit: 1000, after: afterMemberId, ct: ct);
            var memberArray = membersPage.EnumerateArray().ToList();

            if (memberArray.Count == 0) break;

            var userBatch = new List<User>();
            var memberBatch = new List<ServerMember>();
            var memberGroupBatch = new List<MemberGroup>();
            var userMappings = new List<DiscordIdMapping>();

            foreach (var member in memberArray)
            {
                var userObj = member.GetProperty("user");
                var discordUserId = userObj.GetProperty("id").GetString()!;
                var username = userObj.GetProperty("username").GetString() ?? "user";
                var displayName = member.TryGetProperty("nick", out var nickEl) && nickEl.ValueKind != JsonValueKind.Null
                    ? nickEl.GetString() ?? username
                    : (userObj.TryGetProperty("global_name", out var gnEl) && gnEl.ValueKind != JsonValueKind.Null
                        ? gnEl.GetString() ?? username
                        : username);

                var joinedAtStr = member.TryGetProperty("joined_at", out var joinedEl)
                    ? joinedEl.GetString() : null;
                var joinedAt = joinedAtStr != null && DateTimeOffset.TryParse(joinedAtStr, out var jd) ? jd : now;

                // Check if user already exists from a previous checkpoint page
                if (await dbContext.DiscordIdMappings
                    .AnyAsync(m => m.MigrationId == migration.Id
                        && m.EntityType == "User"
                        && m.DiscordId == discordUserId, ct))
                {
                    continue;
                }

                var userId = snowflakeGenerator.NextId();

                // Generate placeholder email - not valid but satisfies the schema
                var placeholderEmail = $"discord-{discordUserId}@migration.local";
                var emailBytes = System.Text.Encoding.UTF8.GetBytes(placeholderEmail);
                var hashBytes = System.Security.Cryptography.SHA256.HashData(emailBytes);

                var user = new User
                {
                    Id = userId,
                    Username = SanitizeUsername(username),
                    DisplayName = displayName[..Math.Min(displayName.Length, 32)],
                    Email = emailBytes,
                    EmailHash = hashBytes,
                    PasswordHash = "$2a$12$migrated-account-placeholder-hash",
                    IsDisabled = false,
                    EmailConfirmed = false,
                    CreatedAt = now
                };
                userBatch.Add(user);

                var serverMember = new ServerMember
                {
                    UserId = userId,
                    ServerId = migration.ServerId,
                    JoinedAt = joinedAt
                };
                memberBatch.Add(serverMember);

                // Role assignments
                if (member.TryGetProperty("roles", out var rolesEl))
                {
                    foreach (var roleIdEl in rolesEl.EnumerateArray())
                    {
                        var discordRoleId = roleIdEl.GetString()!;
                        if (groupLookup.TryGetValue(discordRoleId, out var groupId))
                        {
                            memberGroupBatch.Add(new MemberGroup
                            {
                                UserId = userId,
                                ServerId = migration.ServerId,
                                GroupId = groupId
                            });
                        }
                    }
                }

                userMappings.Add(new DiscordIdMapping
                {
                    Id = snowflakeGenerator.NextId(),
                    MigrationId = migration.Id,
                    DiscordId = discordUserId,
                    XcordId = userId,
                    EntityType = "User"
                });

                afterMemberId = discordUserId;
                totalMigrated++;
            }

            if (userBatch.Count > 0)
            {
                dbContext.Users.AddRange(userBatch);
                dbContext.ServerMembers.AddRange(memberBatch);
                dbContext.MemberGroups.AddRange(memberGroupBatch);
                dbContext.DiscordIdMappings.AddRange(userMappings);
                await dbContext.SaveChangesAsync(ct);
                dbContext.ChangeTracker.Clear();
            }

            // Save checkpoint
            checkpoint.LastMemberDiscordId = afterMemberId;
            migration.CheckpointJson = JsonSerializer.Serialize(checkpoint);
            migration.MigratedMembers = totalMigrated;
            await dbContext.SaveChangesAsync(ct);

            if (memberArray.Count < 1000) break;
        }

        dbContext.ChangeTracker.AutoDetectChangesEnabled = true;

        migration.TotalMembers = totalMigrated;
        migration.MigratedMembers = totalMigrated;
        SetPhaseDone(migration, "Members");
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Migration {MigrationId} Phase 2 complete: {MemberCount} members",
            migration.Id, totalMigrated);
    }

    // -------------------------------------------------------------------------
    // Phase 3 - Messages
    // -------------------------------------------------------------------------

    private async Task RunPhase3MessagesAsync(DiscordMigration migration, MigrationOptions options, CancellationToken ct)
    {
        if (IsPhaseDone(migration, "Messages"))
            return;

        migration.CurrentPhase = "Messages";
        await dbContext.SaveChangesAsync(ct);

        if (await IsStoppedAsync(migration.Id, ct)) return;

        var channelMappings = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "Channel")
            .ToListAsync(ct);

        var userLookup = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "User")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        var checkpoint = migration.CheckpointJson != null
            ? JsonSerializer.Deserialize<MigrationCheckpoint>(migration.CheckpointJson) ?? new MigrationCheckpoint()
            : new MigrationCheckpoint();

        long totalMessages = migration.MigratedMessages;

        // Build channel->conversation lookup
        var channelToConversation = await dbContext.Channels
            .Where(c => c.ServerId == migration.ServerId && c.DeletedAt == null)
            .ToDictionaryAsync(c => c.Id, c => c.ConversationId, ct);

        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        foreach (var channelMap in channelMappings)
        {
            ct.ThrowIfCancellationRequested();
            if (await IsStoppedAsync(migration.Id, ct)) return;

            var xcordChannelId = channelMap.XcordId;
            if (!channelToConversation.TryGetValue(xcordChannelId, out var conversationId))
            {
                logger.LogWarning(
                    "Channel {ChannelId} has no conversation, skipping", xcordChannelId);
                continue;
            }

            // Retrieve per-channel checkpoint cursor
            string? beforeMessageId = checkpoint.ChannelCursors.TryGetValue(channelMap.DiscordId, out var cursor)
                ? cursor : null;

            // If channel is already done, skip
            if (checkpoint.DoneChannels.Contains(channelMap.DiscordId))
                continue;

            totalMessages = await MigrateMessagesForChannelAsync(
                migration, channelMap.DiscordId, conversationId,
                userLookup, options, totalMessages, checkpoint, ct);

            checkpoint.DoneChannels.Add(channelMap.DiscordId);
            migration.MigratedMessages = totalMessages;
            migration.CheckpointJson = JsonSerializer.Serialize(checkpoint);
            await dbContext.SaveChangesAsync(ct);
            dbContext.ChangeTracker.Clear();
        }

        dbContext.ChangeTracker.AutoDetectChangesEnabled = true;

        migration.MigratedMessages = totalMessages;
        SetPhaseDone(migration, "Messages");
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Migration {MigrationId} Phase 3 complete: {MessageCount} messages",
            migration.Id, totalMessages);
    }

    private async Task<long> MigrateMessagesForChannelAsync(
        DiscordMigration migration,
        string discordChannelId,
        long conversationId,
        Dictionary<string, long> userLookup,
        MigrationOptions options,
        long totalMessages,
        MigrationCheckpoint checkpoint,
        CancellationToken ct)
    {
        string? beforeId = checkpoint.ChannelCursors.TryGetValue(discordChannelId, out var c) ? c : null;

        // Maintain a local message ID lookup for reply resolution within this channel
        var localMessageLookup = new Dictionary<string, long>();
        var deferredReplies = new List<(long MessageId, string DiscordReplyId)>();

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var page = await client.GetChannelMessagesAsync(discordChannelId, limit: 100, before: beforeId, ct: ct);
            var msgs = page.EnumerateArray().ToList();
            if (msgs.Count == 0) break;

            var messageBatch = new List<Message>();
            var attachmentBatch = new List<Attachment>();
            var embedBatch = new List<Embed>();
            var mentionBatch = new List<Mention>();
            var msgMappings = new List<DiscordIdMapping>();

            foreach (var msg in msgs)
            {
                var discordMsgId = msg.GetProperty("id").GetString()!;
                var msgType = msg.TryGetProperty("type", out var typeEl) ? typeEl.GetInt32() : 0;
                var content = msg.TryGetProperty("content", out var contentEl)
                    ? contentEl.GetString() ?? "" : "";
                var createdAtStr = msg.TryGetProperty("timestamp", out var tsEl) ? tsEl.GetString() : null;
                var createdAt = createdAtStr != null && DateTimeOffset.TryParse(createdAtStr, out var cd) ? cd
                    : DateTimeOffset.UtcNow;

                long? authorId = null;
                if (msg.TryGetProperty("author", out var authorEl))
                {
                    var discordAuthorId = authorEl.GetProperty("id").GetString()!;
                    if (userLookup.TryGetValue(discordAuthorId, out var xcordUserId))
                        authorId = xcordUserId;
                }

                // Rewrite content mentions
                content = RewriteMentions(content, userLookup, checkpoint.GroupDiscordToXcord,
                    checkpoint.ChannelDiscordToXcord);

                var xcordMsgType = msgType switch
                {
                    7 => MessageType.MemberJoin,
                    19 => MessageType.Default, // reply -> Default
                    _ => MessageType.Default
                };

                // Reply reference
                long? replyToId = null;
                string? discordReplyId = null;
                if (msg.TryGetProperty("message_reference", out var refEl)
                    && refEl.TryGetProperty("message_id", out var refMsgIdEl)
                    && refMsgIdEl.ValueKind != JsonValueKind.Null)
                {
                    discordReplyId = refMsgIdEl.GetString()!;
                    if (localMessageLookup.TryGetValue(discordReplyId, out var localId))
                        replyToId = localId;
                }

                var msgId = snowflakeGenerator.NextId();
                var message = new Message
                {
                    Id = msgId,
                    ConversationId = conversationId,
                    AuthorId = authorId,
                    Type = xcordMsgType,
                    Content = content.Length > 4000 ? content[..4000] : content,
                    ReplyToId = replyToId,
                    EmbedsProcessed = true,
                    CreatedAt = createdAt
                };
                messageBatch.Add(message);
                localMessageLookup[discordMsgId] = msgId;

                if (discordReplyId != null && replyToId == null)
                    deferredReplies.Add((msgId, discordReplyId));

                // Attachments
                if (options.ImportAttachments && msg.TryGetProperty("attachments", out var attchs))
                {
                    foreach (var att in attchs.EnumerateArray())
                    {
                        var attUrl = att.GetProperty("url").GetString()!;
                        var attName = att.TryGetProperty("filename", out var fnEl) ? fnEl.GetString() ?? "file" : "file";
                        var attSize = att.TryGetProperty("size", out var szEl) ? szEl.GetInt64() : 0L;
                        var attContentType = att.TryGetProperty("content_type", out var ctEl)
                            ? ctEl.GetString() ?? "application/octet-stream" : "application/octet-stream";

                        try
                        {
                            using var attStream = await client.DownloadAsync(attUrl, ct);
                            using var ms = new MemoryStream();
                            await attStream.CopyToAsync(ms, ct);
                            var bytes = ms.ToArray();

                            var s3Key = $"attachments/{migration.ServerId}/{msgId}/{discordMsgId}_{attName}";
                            await storageService.UploadAsync(s3Key, bytes, attContentType);

                            var width = att.TryGetProperty("width", out var wEl) && wEl.ValueKind != JsonValueKind.Null
                                ? (int?)wEl.GetInt32() : null;
                            var height = att.TryGetProperty("height", out var hEl) && hEl.ValueKind != JsonValueKind.Null
                                ? (int?)hEl.GetInt32() : null;

                            attachmentBatch.Add(new Attachment
                            {
                                Id = snowflakeGenerator.NextId(),
                                MessageId = msgId,
                                FileName = attName[..Math.Min(attName.Length, 256)],
                                ContentType = attContentType[..Math.Min(attContentType.Length, 128)],
                                FileSize = bytes.Length,
                                S3Key = s3Key,
                                Width = width,
                                Height = height,
                                IsConfirmed = true,
                                CreatedAt = createdAt
                            });
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to migrate attachment {AttUrl}", attUrl);
                        }
                    }
                }

                // Embeds (Discord rich embeds)
                if (msg.TryGetProperty("embeds", out var embeds))
                {
                    int embedPos = 0;
                    foreach (var embed in embeds.EnumerateArray())
                    {
                        var embedUrl = embed.TryGetProperty("url", out var euEl) && euEl.ValueKind != JsonValueKind.Null
                            ? euEl.GetString() ?? "" : "";
                        var embedTitle = embed.TryGetProperty("title", out var etEl) && etEl.ValueKind != JsonValueKind.Null
                            ? etEl.GetString() : null;
                        var embedDesc = embed.TryGetProperty("description", out var edEl) && edEl.ValueKind != JsonValueKind.Null
                            ? edEl.GetString() : null;
                        var embedColor = embed.TryGetProperty("color", out var ecEl) && ecEl.ValueKind != JsonValueKind.Null
                            ? $"#{ecEl.GetInt32():X6}" : null;

                        if (!string.IsNullOrEmpty(embedUrl) || !string.IsNullOrEmpty(embedTitle))
                        {
                            embedBatch.Add(new Embed
                            {
                                Id = snowflakeGenerator.NextId(),
                                MessageId = msgId,
                                Url = embedUrl,
                                Title = embedTitle?[..Math.Min(embedTitle.Length, 256)],
                                Description = embedDesc?[..Math.Min(embedDesc.Length, 4096)],
                                Color = embedColor,
                                Position = embedPos++
                            });
                        }
                    }
                }

                // Mentions
                if (msg.TryGetProperty("mentions", out var userMentions))
                {
                    foreach (var mentionUser in userMentions.EnumerateArray())
                    {
                        var discordMentionId = mentionUser.GetProperty("id").GetString()!;
                        if (userLookup.TryGetValue(discordMentionId, out var xcordMentionId))
                        {
                            mentionBatch.Add(new Mention
                            {
                                Id = snowflakeGenerator.NextId(),
                                MessageId = msgId,
                                MentionedUserId = xcordMentionId
                            });
                        }
                    }
                }

                if (msg.TryGetProperty("mention_everyone", out var everyoneEl) && everyoneEl.GetBoolean())
                {
                    mentionBatch.Add(new Mention
                    {
                        Id = snowflakeGenerator.NextId(),
                        MessageId = msgId,
                        IsEveryone = true
                    });
                }

                msgMappings.Add(new DiscordIdMapping
                {
                    Id = snowflakeGenerator.NextId(),
                    MigrationId = migration.Id,
                    DiscordId = discordMsgId,
                    XcordId = msgId,
                    EntityType = "Message"
                });

                beforeId = discordMsgId; // paginate backwards
                totalMessages++;
            }

            // Persist batch
            dbContext.Messages.AddRange(messageBatch);
            if (attachmentBatch.Count > 0) dbContext.Attachments.AddRange(attachmentBatch);
            if (embedBatch.Count > 0) dbContext.Embeds.AddRange(embedBatch);
            if (mentionBatch.Count > 0) dbContext.Mentions.AddRange(mentionBatch);
            dbContext.DiscordIdMappings.AddRange(msgMappings);
            await dbContext.SaveChangesAsync(ct);
            dbContext.ChangeTracker.Clear();

            // Checkpoint every 1000 messages
            if (totalMessages % 1000 == 0)
            {
                checkpoint.ChannelCursors[discordChannelId] = beforeId!;
                migration.MigratedMessages = totalMessages;
                migration.CheckpointJson = JsonSerializer.Serialize(checkpoint);
                await dbContext.SaveChangesAsync(ct);
            }

            if (msgs.Count < 100) break;
        }

        // Resolve deferred replies now that we have the full message mapping in DB
        if (deferredReplies.Count > 0)
        {
            var discordReplyIds = deferredReplies.Select(r => r.DiscordReplyId).Distinct().ToList();
            var resolvedReplies = await dbContext.DiscordIdMappings
                .Where(m => m.MigrationId == migration.Id
                    && m.EntityType == "Message"
                    && discordReplyIds.Contains(m.DiscordId))
                .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

            foreach (var (msgId, discordReplyId) in deferredReplies)
            {
                if (resolvedReplies.TryGetValue(discordReplyId, out var xcordReplyId))
                {
                    var msgToUpdate = await dbContext.Messages.FindAsync([msgId], ct);
                    if (msgToUpdate != null)
                    {
                        msgToUpdate.ReplyToId = xcordReplyId;
                    }
                }
            }

            await dbContext.SaveChangesAsync(ct);
            dbContext.ChangeTracker.Clear();
        }

        return totalMessages;
    }

    // -------------------------------------------------------------------------
    // Phase 4 - Threads
    // -------------------------------------------------------------------------

    private async Task RunPhase4ThreadsAsync(DiscordMigration migration, MigrationOptions options, CancellationToken ct)
    {
        if (IsPhaseDone(migration, "Threads"))
            return;

        migration.CurrentPhase = "Threads";
        await dbContext.SaveChangesAsync(ct);

        if (await IsStoppedAsync(migration.Id, ct)) return;

        var userLookup = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "User")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        var channelMappings = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "Channel")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        var checkpoint = migration.CheckpointJson != null
            ? JsonSerializer.Deserialize<MigrationCheckpoint>(migration.CheckpointJson) ?? new MigrationCheckpoint()
            : new MigrationCheckpoint();

        var now = DateTimeOffset.UtcNow;

        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        // Fetch active threads
        var activeThreadsResult = await client.GetActiveThreadsAsync(migration.DiscordGuildId, ct);
        var allThreads = new List<System.Text.Json.JsonElement>();

        if (activeThreadsResult.TryGetProperty("threads", out var activeThreadsEl))
            allThreads.AddRange(activeThreadsEl.EnumerateArray());

        // Fetch archived threads per channel
        foreach (var channelDiscordId in channelMappings.Keys)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var archivedResult = await client.GetArchivedThreadsAsync(channelDiscordId, ct);
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
            if (await IsStoppedAsync(migration.Id, ct)) break;

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
                var threadMembers = await client.GetThreadMembersAsync(discordThreadId, ct);
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

            await dbContext.SaveChangesAsync(ct);
            dbContext.ChangeTracker.Clear();

            // Migrate thread messages
            totalMessages = await MigrateMessagesForChannelAsync(
                migration, discordThreadId, convId,
                userLookup, options, totalMessages, checkpoint, ct);

            checkpoint.DoneChannels.Add($"thread:{discordThreadId}");
            migration.MigratedMessages = totalMessages;
            migration.CheckpointJson = JsonSerializer.Serialize(checkpoint);
            await dbContext.SaveChangesAsync(ct);
            dbContext.ChangeTracker.Clear();
        }

        dbContext.ChangeTracker.AutoDetectChangesEnabled = true;

        migration.MigratedMessages = totalMessages;
        SetPhaseDone(migration, "Threads");
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Migration {MigrationId} Phase 4 complete: {ThreadCount} threads processed",
            migration.Id, allThreads.Count);
    }

    // -------------------------------------------------------------------------
    // Phase 5 - Finalization
    // -------------------------------------------------------------------------

    private async Task RunPhase5FinalizationAsync(DiscordMigration migration, CancellationToken ct)
    {
        migration.CurrentPhase = "Finalization";
        await dbContext.SaveChangesAsync(ct);

        var memberCount = await dbContext.ServerMembers
            .CountAsync(m => m.ServerId == migration.ServerId && m.DeletedAt == null, ct);

        var server = await dbContext.Servers.FindAsync([migration.ServerId], ct);
        if (server != null)
        {
            server.MemberCount = memberCount;
            await dbContext.SaveChangesAsync(ct);
        }

        migration.Status = "Completed";
        migration.CompletedAt = DateTimeOffset.UtcNow;
        migration.TotalMembers = memberCount;
        migration.MigratedMembers = memberCount;
        SetPhaseDone(migration, "Finalization");
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Migration {MigrationId} completed successfully for server {ServerId}",
            migration.Id, migration.ServerId);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool IsPhaseDone(DiscordMigration migration, string phase)
    {
        if (migration.CheckpointJson == null) return false;
        var checkpoint = JsonSerializer.Deserialize<MigrationCheckpoint>(migration.CheckpointJson);
        return checkpoint?.DonePhases.Contains(phase) ?? false;
    }

    private static void SetPhaseDone(DiscordMigration migration, string phase)
    {
        var checkpoint = migration.CheckpointJson != null
            ? JsonSerializer.Deserialize<MigrationCheckpoint>(migration.CheckpointJson) ?? new MigrationCheckpoint()
            : new MigrationCheckpoint();

        checkpoint.DonePhases.Add(phase);
        migration.CheckpointJson = JsonSerializer.Serialize(checkpoint);
    }

    private async Task<bool> IsStoppedAsync(long migrationId, CancellationToken ct)
    {
        // Reload from DB to detect cancel
        var current = await dbContext.DiscordMigrations
            .AsNoTracking()
            .Where(m => m.Id == migrationId)
            .Select(m => m.Status)
            .FirstOrDefaultAsync(ct);
        return current != "Running";
    }

    private static string RewriteMentions(
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

    private static string SanitizeUsername(string username)
    {
        // Ensure username is valid: lowercase, alphanumeric + underscore, max 32 chars
        var sanitized = Regex.Replace(username.ToLowerInvariant(), @"[^a-z0-9_]", "_");
        if (sanitized.Length > 32) sanitized = sanitized[..32];
        if (string.IsNullOrEmpty(sanitized)) sanitized = "user";
        return sanitized;
    }

    private async Task TrySaveAsync(DiscordMigration migration, CancellationToken ct)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save migration state for {MigrationId}", migration.Id);
        }
    }
}

// -------------------------------------------------------------------------
// Supporting DTOs
// -------------------------------------------------------------------------

public sealed class MigrationOptions
{
    public bool ImportMessages { get; set; } = true;
    public bool ImportAttachments { get; set; } = true;
    public bool ImportReactions { get; set; } = false;
    public bool ImportThreads { get; set; } = true;
    public bool ImportEmoji { get; set; } = true;
    public bool ImportAvatars { get; set; } = false;
}

public sealed class MigrationCheckpoint
{
    public HashSet<string> DonePhases { get; set; } = new();
    public HashSet<string> DoneChannels { get; set; } = new();
    public Dictionary<string, string> ChannelCursors { get; set; } = new();
    public string? LastMemberDiscordId { get; set; }
    public Dictionary<string, long> GroupDiscordToXcord { get; set; } = new();
    public Dictionary<string, long> ChannelDiscordToXcord { get; set; } = new();
}
