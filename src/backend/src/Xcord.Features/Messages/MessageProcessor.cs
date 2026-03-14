using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord;

namespace Xcord.Features.Messages;

/// <summary>
/// Message processing pipeline implementation.
/// Processes messages through sanitization, validation, automod, mention parsing, URL extraction,
/// and slowmode enforcement.
/// </summary>
public sealed class MessageProcessor : IMessageProcessor
{
    private readonly AppDbContext _dbContext;
    private readonly SnowflakeIdGenerator _snowflakeGenerator;
    private readonly IAutomodService _automodService;
    private readonly ISlowmodeService _slowmodeService;
    private readonly IRoleService _roleService;

    // Regex patterns for mention parsing and URL extraction
    // Note: Content is HTML-encoded before mention parsing, so angle brackets become &lt; and &gt;
    private static readonly Regex UserMentionRegex = new(@"&lt;@(\d+)&gt;", RegexOptions.Compiled);
    private static readonly Regex RoleMentionRegex = new(@"&lt;@&amp;(\d+)&gt;", RegexOptions.Compiled);
    private static readonly Regex EveryoneMentionRegex = new(@"@everyone", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex UrlRegex = new(@"https?://[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CustomEmojiRegex = new(@"&lt;(a?):([a-zA-Z0-9_]+):(\d+)&gt;", RegexOptions.Compiled);

    public MessageProcessor(
        AppDbContext dbContext,
        SnowflakeIdGenerator snowflakeGenerator,
        IAutomodService automodService,
        ISlowmodeService slowmodeService,
        IRoleService roleService)
    {
        _dbContext = dbContext;
        _snowflakeGenerator = snowflakeGenerator;
        _automodService = automodService;
        _slowmodeService = slowmodeService;
        _roleService = roleService;
    }

    public async Task<Result<MessageProcessingResult>> ProcessAsync(Message message, long serverId, long channelId, IEnumerable<long> authorGroupIds, bool isBot)
    {
        // Step 1: Sanitize content
        SanitizeContent(message);

        // Step 2: Validate content
        var validationResult = ValidateMessage(message);
        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        // Step 3: Automod checks (evaluate before persisting)
        var automodResult = await _automodService.EvaluateAsync(
            message.Content,
            serverId,
            channelId,
            message.AuthorId ?? 0,
            authorGroupIds,
            isBot);

        if (!automodResult.IsAllowed)
        {
            return Error.Forbidden("AUTOMOD_BLOCKED", automodResult.BlockReason ?? "Message blocked by automod");
        }

        // Step 4: Validate and sanitize custom emoji
        await ValidateCustomEmoji(message, serverId);

        // Step 5: Parse mentions
        await ParseMentions(message, serverId);

        // Step 6: Extract URLs for future embed processing
        ExtractUrls(message);

        // Step 7: Slowmode enforcement
        var slowmodeResult = await EnforceSlowmodeAsync(message, channelId, isBot);
        if (slowmodeResult.IsFailure)
        {
            return slowmodeResult.Error;
        }

        return new MessageProcessingResult
        {
            Message = message,
            DeferredActions = automodResult.DeferredActions
        };
    }

    /// <summary>
    /// Sanitizes message content by HTML encoding and trimming whitespace.
    /// </summary>
    private void SanitizeContent(Message message)
    {
        // HTML encode to prevent XSS
        message.Content = HtmlEncoder.Default.Encode(message.Content);

        // Trim excess whitespace
        message.Content = message.Content.Trim();
    }

    /// <summary>
    /// Validates message content and type rules.
    /// </summary>
    private Result<Message> ValidateMessage(Message message)
    {
        // Check content length
        if (string.IsNullOrWhiteSpace(message.Content))
        {
            return Error.Validation("EMPTY_MESSAGE", "Message content cannot be empty");
        }

        if (message.Content.Length > 4000)
        {
            return Error.Validation("MESSAGE_TOO_LONG", "Message content must not exceed 4000 characters");
        }

        // Validate message type rules
        if (message.Type != MessageType.Default && message.Type != MessageType.PollCreated)
        {
            // System messages should not be sent by users
            return Error.Validation("INVALID_MESSAGE_TYPE", "Invalid message type for user messages");
        }

        return message;
    }

    /// <summary>
    /// Parses @user, @role, and @everyone mentions from message content.
    /// Creates Mention entities for each mention found.
    /// Batch-loads valid user and role IDs in a single query each to avoid N+1 DB hits.
    /// </summary>
    private async Task ParseMentions(Message message, long serverId)
    {
        var mentions = new List<Mention>();

        // Parse @everyone mentions
        if (EveryoneMentionRegex.IsMatch(message.Content))
        {
            mentions.Add(new Mention
            {
                Id = _snowflakeGenerator.NextId(),
                MessageId = message.Id,
                IsEveryone = true
            });
        }

        // Collect all candidate user IDs from the message before hitting the DB
        var userMatches = UserMentionRegex.Matches(message.Content);
        var candidateUserIds = userMatches
            .Select(m => long.TryParse(m.Groups[1].Value, out var id) ? id : (long?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        // Batch-load all valid member IDs in a single query to avoid per-mention round-trips
        if (candidateUserIds.Count > 0)
        {
            var validUserIds = await _dbContext.ServerMembers
                .AsNoTracking()
                .Where(sm => sm.ServerId == serverId && candidateUserIds.Contains(sm.UserId))
                .Select(sm => sm.UserId)
                .ToListAsync();

            var validUserIdSet = new HashSet<long>(validUserIds);

            foreach (Match match in userMatches)
            {
                if (long.TryParse(match.Groups[1].Value, out var userId) && validUserIdSet.Contains(userId))
                {
                    mentions.Add(new Mention
                    {
                        Id = _snowflakeGenerator.NextId(),
                        MessageId = message.Id,
                        MentionedUserId = userId
                    });
                }
            }
        }

        // Collect all candidate role IDs from the message before hitting the DB
        var roleMatches = RoleMentionRegex.Matches(message.Content);
        var candidateRoleIds = roleMatches
            .Select(m => long.TryParse(m.Groups[1].Value, out var id) ? id : (long?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        // Batch-load all valid role IDs in a single query to avoid per-mention round-trips
        if (candidateRoleIds.Count > 0)
        {
            var validRoleIds = await _dbContext.Groups
                .AsNoTracking()
                .Where(r => r.ServerId == serverId && candidateRoleIds.Contains(r.Id))
                .Select(r => r.Id)
                .ToListAsync();

            var validRoleIdSet = new HashSet<long>(validRoleIds);

            foreach (Match match in roleMatches)
            {
                if (long.TryParse(match.Groups[1].Value, out var roleId) && validRoleIdSet.Contains(roleId))
                {
                    mentions.Add(new Mention
                    {
                        Id = _snowflakeGenerator.NextId(),
                        MessageId = message.Id,
                        MentionedGroupId = roleId
                    });
                }
            }
        }

        message.Mentions = mentions;
    }

    /// <summary>
    /// Validates custom emoji references and strips invalid ones.
    /// Pattern: &lt;:name:id&gt; for static, &lt;a:name:id&gt; for animated.
    /// Batch-loads all referenced emoji in a single query to avoid N+1 DB hits.
    /// </summary>
    private async Task ValidateCustomEmoji(Message message, long serverId)
    {
        var emojiMatches = CustomEmojiRegex.Matches(message.Content);
        if (emojiMatches.Count == 0)
        {
            return;
        }

        // Collect all candidate emoji IDs before hitting the DB
        var candidateEmojiIds = emojiMatches
            .Select(m => long.TryParse(m.Groups[3].Value, out var id) ? id : (long?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        // Batch-load all referenced emoji in a single query to avoid per-emoji round-trips
        var emojiById = candidateEmojiIds.Count > 0
            ? await _dbContext.CustomEmojis
                .AsNoTracking()
                .Where(e => candidateEmojiIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id)
            : new Dictionary<long, CustomEmoji>();

        // Batch-load server membership for all distinct servers referenced by the emoji
        var referencedServerIds = emojiById.Values.Select(e => e.ServerId).Distinct().ToList();
        var accessibleServerIds = referencedServerIds.Count > 0
            ? await _dbContext.ServerMembers
                .AsNoTracking()
                .Where(sm => sm.UserId == message.AuthorId && referencedServerIds.Contains(sm.ServerId))
                .Select(sm => sm.ServerId)
                .ToListAsync()
            : new List<long>();

        var accessibleServerIdSet = new HashSet<long>(accessibleServerIds);

        foreach (Match match in emojiMatches)
        {
            var isAnimated = match.Groups[1].Value == "a";
            var emojiName = match.Groups[2].Value;
            var emojiIdStr = match.Groups[3].Value;

            if (!long.TryParse(emojiIdStr, out var emojiId))
            {
                // Invalid ID format - replace with plain text
                message.Content = message.Content.Replace(match.Value, $":{emojiName}:");
                continue;
            }

            // Use the pre-loaded emoji dictionary (no DB hit per iteration)
            if (!emojiById.TryGetValue(emojiId, out var emoji))
            {
                // Emoji doesn't exist - replace with plain text
                message.Content = message.Content.Replace(match.Value, $":{emojiName}:");
                continue;
            }

            // Use the pre-loaded server membership set (no DB hit per iteration)
            if (!accessibleServerIdSet.Contains(emoji.ServerId))
            {
                // User doesn't have access to this emoji - replace with plain text
                message.Content = message.Content.Replace(match.Value, $":{emojiName}:");
                continue;
            }

            // Verify animated flag matches
            if (emoji.IsAnimated != isAnimated)
            {
                // Mismatch in animated flag - replace with plain text
                message.Content = message.Content.Replace(match.Value, $":{emojiName}:");
            }

            // If we reach here, emoji is valid - leave it in the message
        }
    }

    /// <summary>
    /// Extracts URLs from message content and stores in metadata for future embed processing.
    /// </summary>
    private void ExtractUrls(Message message)
    {
        var urlMatches = UrlRegex.Matches(message.Content);
        if (urlMatches.Count > 0)
        {
            var urls = urlMatches.Select(m => m.Value).Distinct().ToList();

            // Store URLs in metadata as JSON
            var metadata = new
            {
                urls = urls
            };

            message.Metadata = JsonSerializer.Serialize(metadata);
        }
    }

    /// <summary>
    /// Enforces the channel's slowmode setting.
    /// Bots and users with ManageMessages or ManageChannels are exempt.
    /// Returns a RateLimited error with the remaining wait seconds if the user is in cooldown.
    /// </summary>
    private async Task<Result<bool>> EnforceSlowmodeAsync(Message message, long channelId, bool isBot)
    {
        // Load the channel to get SlowModeSeconds
        var channel = await _dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == channelId);

        // No channel (e.g. thread with no parent channel) or slowmode disabled - nothing to do
        if (channel == null || !channel.SlowModeSeconds.HasValue || channel.SlowModeSeconds.Value <= 0)
        {
            return true;
        }

        // Bots are exempt from slowmode
        if (isBot)
        {
            return true;
        }

        // Users with ManageMessages or ManageChannels bypass slowmode
        var authorId = message.AuthorId ?? 0;
        if (authorId > 0)
        {
            var channelPermissions = await _roleService.GetChannelRoles(authorId, channelId);
            var bypassPermissions = (long)(Role.ManageMessages | Role.ManageChannels);
            if ((channelPermissions & bypassPermissions) != 0)
            {
                return true;
            }
        }

        // Check Redis for an active cooldown and record this send if allowed
        var remainingSeconds = await _slowmodeService.CheckAndRecordAsync(
            authorId, channelId, channel.SlowModeSeconds.Value);

        if (remainingSeconds > 0)
        {
            return Error.RateLimited(
                "SLOWMODE_RATE_LIMITED",
                remainingSeconds.ToString());
        }

        return true;
    }
}
