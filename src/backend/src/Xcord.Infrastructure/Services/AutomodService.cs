using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Redis-backed implementation of automod service.
/// Evaluates messages against automod rules and performs rate limiting checks.
/// </summary>
public sealed class AutomodService : IAutomodService
{
    private readonly AppDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly string _prefix;

    // Regex for extracting URLs from message content
    private static readonly Regex UrlRegex = new(@"https?://([^/\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for extracting mentions from message content
    private static readonly Regex MentionRegex = new(@"<@(\d+)>|<@&(\d+)>|@everyone", RegexOptions.Compiled);

    // JSON options for trigger/action config deserialization (configs use camelCase keys)
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AutomodService(
        AppDbContext dbContext,
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions)
    {
        _dbContext = dbContext;
        _redis = redis;
        _prefix = redisOptions.Value.ChannelPrefix;
    }

    public async Task<AutomodResult> EvaluateAsync(
        string messageContent,
        long serverId,
        long channelId,
        long authorId,
        IEnumerable<long> authorGroupIds,
        bool isBot,
        CancellationToken cancellationToken = default)
    {
        // Load all enabled rules for this server (channel-specific and server-wide)
        var allRules = await _dbContext.AutomodRules
            .AsNoTracking()
            .Where(r => r.ServerId == serverId && r.Enabled)
            .ToListAsync(cancellationToken);

        if (allRules.Count == 0)
        {
            return AutomodResult.Allowed();
        }

        // Channel-specific rules take precedence over server-wide rules.
        // If any channel-specific rules exist for this channel, only those are evaluated.
        // Server-wide rules (ChannelId == null) are the fallback when no channel-specific rules match.
        var channelRules = allRules.Where(r => r.ChannelId == channelId).ToList();
        var rules = channelRules.Count > 0
            ? channelRules
            : allRules.Where(r => r.ChannelId == null).ToList();

        if (rules.Count == 0)
        {
            return AutomodResult.Allowed();
        }

        var deferredActions = new List<AutomodDeferredAction>();
        var authorGroupIdSet = new HashSet<long>(authorGroupIds);

        foreach (var rule in rules)
        {
            // Check exemptions
            if (IsExempt(rule, channelId, authorGroupIdSet, isBot))
            {
                continue;
            }

            // Evaluate the rule trigger
            var triggered = await EvaluateTriggerAsync(rule, messageContent, serverId, authorId, cancellationToken);

            if (triggered)
            {
                // BlockMessage actions should block immediately
                if (rule.ActionType == AutomodActionType.BlockMessage)
                {
                    return AutomodResult.Blocked($"Message blocked by automod rule: {rule.Name}");
                }

                // Other actions are deferred until after message persistence
                var config = ParseActionConfig(rule.ActionConfig);
                deferredActions.Add(new AutomodDeferredAction
                {
                    ActionType = rule.ActionType,
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Config = config
                });
            }
        }

        return deferredActions.Count > 0
            ? AutomodResult.WithActions(deferredActions)
            : AutomodResult.Allowed();
    }

    /// <summary>
    /// Checks if a user/channel is exempt from this rule.
    /// </summary>
    private bool IsExempt(
        Entities.AutomodRule rule,
        long channelId,
        HashSet<long> authorGroupIds,
        bool isBot)
    {
        // Check bot exemption
        if (isBot && rule.ExemptBots)
        {
            return true;
        }

        // Check channel exemption
        if (!string.IsNullOrEmpty(rule.ExemptChannelIds) &&
            rule.ExemptChannelIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(long.Parse)
                .Contains(channelId))
        {
            return true;
        }

        // Check role exemption
        if (!string.IsNullOrEmpty(rule.ExemptRoleIds))
        {
            var exemptRoles = rule.ExemptRoleIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(long.Parse)
                .ToHashSet();

            if (exemptRoles.Overlaps(authorGroupIds))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Evaluates a rule's trigger condition.
    /// </summary>
    private async Task<bool> EvaluateTriggerAsync(
        Entities.AutomodRule rule,
        string messageContent,
        long serverId,
        long authorId,
        CancellationToken cancellationToken)
    {
        return rule.TriggerType switch
        {
            AutomodTriggerType.Keyword => EvaluateKeywordTrigger(rule.TriggerConfig, messageContent),
            AutomodTriggerType.Regex => EvaluateRegexTrigger(rule.TriggerConfig, messageContent),
            AutomodTriggerType.MentionSpam => EvaluateMentionSpamTrigger(rule.TriggerConfig, messageContent),
            AutomodTriggerType.MessageSpam => await EvaluateMessageSpamTriggerAsync(rule.TriggerConfig, serverId, authorId, cancellationToken),
            AutomodTriggerType.LinkFilter => EvaluateLinkFilterTrigger(rule.TriggerConfig, messageContent),
            _ => false
        };
    }

    /// <summary>
    /// Evaluates keyword trigger.
    /// </summary>
    private bool EvaluateKeywordTrigger(string triggerConfig, string messageContent)
    {
        var config = JsonSerializer.Deserialize<KeywordTriggerConfig>(triggerConfig, JsonOptions);
        if (config?.Keywords == null || config.Keywords.Length == 0)
        {
            return false;
        }

        var comparison = StringComparison.OrdinalIgnoreCase;

        if (config.MatchWholeWord)
        {
            // Split message into words and check each
            var words = messageContent.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return config.Keywords.Any(keyword => words.Any(word => word.Equals(keyword, comparison)));
        }
        else
        {
            // Simple substring match
            return config.Keywords.Any(keyword => messageContent.Contains(keyword, comparison));
        }
    }

    /// <summary>
    /// Evaluates regex trigger.
    /// </summary>
    private bool EvaluateRegexTrigger(string triggerConfig, string messageContent)
    {
        var config = JsonSerializer.Deserialize<RegexTriggerConfig>(triggerConfig, JsonOptions);
        if (string.IsNullOrEmpty(config?.Pattern))
        {
            return false;
        }

        try
        {
            var options = config.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            var regex = new Regex(config.Pattern, options | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
            return regex.IsMatch(messageContent);
        }
        catch
        {
            // Invalid regex or timeout
            return false;
        }
    }

    /// <summary>
    /// Evaluates mention spam trigger.
    /// </summary>
    private bool EvaluateMentionSpamTrigger(string triggerConfig, string messageContent)
    {
        var config = JsonSerializer.Deserialize<MentionSpamTriggerConfig>(triggerConfig, JsonOptions);
        if (config?.MaxMentions == null || config.MaxMentions <= 0)
        {
            return false;
        }

        var mentionMatches = MentionRegex.Matches(messageContent);
        return mentionMatches.Count > config.MaxMentions;
    }

    /// <summary>
    /// Evaluates message spam trigger using Redis rate limiting.
    /// </summary>
    private async Task<bool> EvaluateMessageSpamTriggerAsync(
        string triggerConfig,
        long serverId,
        long authorId,
        CancellationToken cancellationToken)
    {
        var config = JsonSerializer.Deserialize<MessageSpamTriggerConfig>(triggerConfig, JsonOptions);
        if (config?.MaxMessages == null || config.MaxMessages <= 0 || config.IntervalSeconds <= 0)
        {
            return false;
        }

        var db = _redis.GetDatabase();
        var key = $"{_prefix}:automod:spam:{authorId}:{serverId}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - config.IntervalSeconds;

        // Remove old entries outside the time window
        await db.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, windowStart);

        // Add current message timestamp
        await db.SortedSetAddAsync(key, now, now);

        // Set expiration on the key
        await db.KeyExpireAsync(key, TimeSpan.FromSeconds(config.IntervalSeconds));

        // Count messages in the window
        var count = await db.SortedSetLengthAsync(key);

        return count > config.MaxMessages;
    }

    /// <summary>
    /// Evaluates link filter trigger.
    /// </summary>
    private bool EvaluateLinkFilterTrigger(string triggerConfig, string messageContent)
    {
        var config = JsonSerializer.Deserialize<LinkFilterTriggerConfig>(triggerConfig, JsonOptions);
        if (config == null)
        {
            return false;
        }

        var urlMatches = UrlRegex.Matches(messageContent);
        if (urlMatches.Count == 0)
        {
            return false; // No URLs to filter
        }

        var domains = urlMatches
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .ToList();

        // If there's a blocklist, check if any domain is blocked
        if (config.BlockedDomains?.Length > 0)
        {
            var blockedSet = config.BlockedDomains.Select(d => d.ToLowerInvariant()).ToHashSet();
            if (domains.Any(d => blockedSet.Contains(d)))
            {
                return true; // Blocked domain found
            }
        }

        // If there's an allowlist, check if all domains are allowed
        if (config.AllowedDomains?.Length > 0)
        {
            var allowedSet = config.AllowedDomains.Select(d => d.ToLowerInvariant()).ToHashSet();
            if (domains.Any(d => !allowedSet.Contains(d)))
            {
                return true; // Non-allowed domain found
            }
        }

        return false;
    }

    /// <summary>
    /// Parses action config JSON into a dictionary.
    /// </summary>
    private Dictionary<string, object>? ParseActionConfig(string? actionConfig)
    {
        if (string.IsNullOrEmpty(actionConfig))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(actionConfig, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    // Trigger config classes
    private sealed class KeywordTriggerConfig
    {
        public string[]? Keywords { get; set; }
        public bool MatchWholeWord { get; set; }
    }

    private sealed class RegexTriggerConfig
    {
        public string? Pattern { get; set; }
        public bool CaseSensitive { get; set; }
    }

    private sealed class MentionSpamTriggerConfig
    {
        public int MaxMentions { get; set; }
    }

    private sealed class MessageSpamTriggerConfig
    {
        public int MaxMessages { get; set; }
        public int IntervalSeconds { get; set; }
    }

    private sealed class LinkFilterTriggerConfig
    {
        public string[]? AllowedDomains { get; set; }
        public string[]? BlockedDomains { get; set; }
    }
}
