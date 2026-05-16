using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;

namespace Xcord.Infrastructure.Services.Discord;

/// <summary>
/// Paginates Discord channel messages and persists them as Xcord messages,
/// attachments, embeds, and mentions. Used by both the Messages phase
/// (regular channels) and the Threads phase (one call per thread).
/// </summary>
public sealed class MessageChannelMigrator
{
    private readonly MigrationPhaseContext _ctx;

    public MessageChannelMigrator(MigrationPhaseContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<long> MigrateAsync(
        DiscordMigration migration,
        string discordChannelId,
        long conversationId,
        Dictionary<string, long> userLookup,
        MigrationOptions options,
        long totalMessages,
        MigrationCheckpoint checkpoint,
        CancellationToken ct)
    {
        var dbContext = _ctx.DbContext;
        var client = _ctx.Client;
        var snowflakeGenerator = _ctx.SnowflakeGenerator;
        var storageService = _ctx.StorageService;
        var logger = _ctx.Logger;

        string? beforeId = checkpoint.ChannelCursors.TryGetValue(discordChannelId, out var c) ? c : null;

        // Maintain a local message ID lookup for reply resolution within this channel
        var localMessageLookup = new Dictionary<string, long>();
        var deferredReplies = new List<(long MessageId, string DiscordReplyId)>();

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var page = await client.GetChannelMessagesAsync(discordChannelId, limit: 100, before: beforeId, ct: ct).ConfigureAwait(false);
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
                content = MigrationPhaseContext.RewriteMentions(content, userLookup,
                    checkpoint.GroupDiscordToXcord, checkpoint.ChannelDiscordToXcord);

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
                            using var attStream = await client.DownloadAsync(attUrl, ct).ConfigureAwait(false);
                            using var ms = new MemoryStream();
                            await attStream.CopyToAsync(ms, ct).ConfigureAwait(false);
                            var bytes = ms.ToArray();

                            var s3Key = $"attachments/{migration.ServerId}/{msgId}/{discordMsgId}_{attName}";
                            await storageService.UploadAsync(s3Key, bytes, attContentType).ConfigureAwait(false);

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
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();

            // Checkpoint every 1000 messages
            if (totalMessages % 1000 == 0)
            {
                checkpoint.ChannelCursors[discordChannelId] = beforeId!;
                migration.MigratedMessages = totalMessages;
                migration.CheckpointJson = JsonSerializer.Serialize(checkpoint);
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
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
                    var msgToUpdate = await dbContext.Messages.FindAsync([msgId], ct).ConfigureAwait(false);
                    if (msgToUpdate != null)
                    {
                        msgToUpdate.ReplyToId = xcordReplyId;
                    }
                }
            }

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
        }

        return totalMessages;
    }
}
