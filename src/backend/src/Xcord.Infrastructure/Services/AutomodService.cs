using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Globalization;
using System.Text;
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

    // Regex for extracting URLs from message content (raw URLs)
    private static readonly Regex UrlRegex = new(@"https?://([^/\s)]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for extracting markdown link destinations: [label](https://example.com)
    private static readonly Regex MarkdownUrlRegex = new(@"\[[^\]]*\]\((https?://[^)\s]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for extracting mentions from message content
    private static readonly Regex MentionRegex = new(@"<@(\d+)>|<@&(\d+)>|@everyone", RegexOptions.Compiled);

    // JSON options for trigger/action config deserialization (configs use camelCase keys)
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Cache of compiled Regex instances keyed by (pattern, options).
    // Avoids reconstructing the regex (and re-running NonBacktracking analysis) on every message.
    // MemoryCache enforces a per-entry LRU eviction when the SizeLimit is hit (each entry counts
    // as size 1, so SizeLimit doubles as a max-entries cap). Entries also expire after 1 hour of
    // disuse so a one-off pattern does not linger forever.
    private const int RegexCacheLimit = 1000;
    private static readonly MemoryCache RegexCache = new(new MemoryCacheOptions
    {
        SizeLimit = RegexCacheLimit
    });
    private static readonly MemoryCacheEntryOptions RegexEntryOptions = new()
    {
        Size = 1,
        SlidingExpiration = TimeSpan.FromHours(1)
    };

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
        // Channel-specific rules take precedence over server-wide rules.
        // Push the (ServerId, Enabled, ChannelId) filter into SQL so the DB returns only
        // rules potentially applicable to this channel: either explicitly bound to it or
        // server-wide (ChannelId IS NULL). The precedence (channel-specific overrides
        // server-wide) is then resolved in-memory on the already-narrowed result set.
        var candidateRules = await _dbContext.AutomodRules
            .AsNoTracking()
            .Where(r => r.ServerId == serverId
                        && r.Enabled
                        && (r.ChannelId == channelId || r.ChannelId == null))
            .ToListAsync(cancellationToken);

        if (candidateRules.Count == 0)
        {
            return AutomodResult.Allowed();
        }

        // Channel-specific rules take precedence: if any exist, only those are evaluated.
        // Otherwise fall back to server-wide rules (ChannelId == null).
        var channelRules = candidateRules.Where(r => r.ChannelId == channelId).ToList();
        var rules = channelRules.Count > 0
            ? channelRules
            : candidateRules.Where(r => r.ChannelId == null).ToList();

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
            var triggered = await EvaluateTriggerAsync(rule, messageContent, serverId, authorId, cancellationToken).ConfigureAwait(false);

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

        // Normalize both the message and the configured keywords so attempts to bypass the
        // filter via fullwidth characters, ligatures, or zero-width separators get caught.
        var normalizedMessage = NormalizeForMatching(messageContent);
        var normalizedKeywords = config.Keywords
            .Select(NormalizeForMatching)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToArray();

        if (normalizedKeywords.Length == 0)
        {
            return false;
        }

        if (config.MatchWholeWord)
        {
            // Split message into words and check each
            var words = normalizedMessage.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return normalizedKeywords.Any(keyword => words.Any(word => word.Equals(keyword, comparison)));
        }
        else
        {
            // Simple substring match
            return normalizedKeywords.Any(keyword => normalizedMessage.Contains(keyword, comparison));
        }
    }

    /// <summary>
    /// Normalizes a string for safe matching. NFKC collapses compatibility characters
    /// (fullwidth, ligatures, etc.) and decomposes combining marks. Strips Unicode
    /// format/control characters (zero-width spaces, BOM, etc.) so they cannot be
    /// used to split filtered tokens.
    /// </summary>
    private static string NormalizeForMatching(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var normalized = input.Normalize(NormalizationForm.FormKC);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.Format || cat == UnicodeCategory.Control) continue;
            sb.Append(ch);
        }
        return sb.ToString();
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

        var baseOptions = config.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

        try
        {
            var regex = GetOrBuildRegex(config.Pattern, baseOptions);
            return regex.IsMatch(messageContent);
        }
        catch
        {
            // Invalid regex or timeout
            return false;
        }
    }

    /// <summary>
    /// Returns a cached compiled Regex for the (pattern, options) pair. Prefers
    /// RegexOptions.NonBacktracking (linear-time, immune to catastrophic backtracking).
    /// Falls back to a strict 20ms timeout if the pattern uses features incompatible
    /// with NonBacktracking (lookaround, backreferences, etc.).
    /// </summary>
    private static Regex GetOrBuildRegex(string pattern, RegexOptions baseOptions)
    {
        var cacheKey = $"{(int)baseOptions}:{pattern}";

        if (RegexCache.TryGetValue(cacheKey, out Regex? cached) && cached is not null)
        {
            return cached;
        }

        Regex built;
        try
        {
            // NonBacktracking guarantees linear time evaluation; the 100ms timeout is a safety net.
            built = new Regex(pattern, baseOptions | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            // Pattern uses features NonBacktracking does not support (lookaround, backreferences, etc.).
            // Fall back to the standard backtracking engine with a tighter 20ms timeout so a pathological
            // pattern can do less damage.
            built = new Regex(pattern, baseOptions | RegexOptions.Compiled, TimeSpan.FromMilliseconds(20));
        }

        // MemoryCache enforces SizeLimit by evicting the least-recently-used entry on insert.
        // Each entry has Size = 1 so the cap is "max 1000 distinct patterns".
        RegexCache.Set(cacheKey, built, RegexEntryOptions);
        return built;
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

        // Normalize first so zero-width spaces inserted inside <@1234> mentions are stripped
        // before the mention regex runs.
        var normalized = NormalizeForMatching(messageContent);
        var mentionMatches = MentionRegex.Matches(normalized);
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
        await db.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, windowStart).ConfigureAwait(false);

        // Add current message timestamp
        await db.SortedSetAddAsync(key, now, now).ConfigureAwait(false);

        // Set expiration on the key
        await db.KeyExpireAsync(key, TimeSpan.FromSeconds(config.IntervalSeconds)).ConfigureAwait(false);

        // Count messages in the window
        var count = await db.SortedSetLengthAsync(key).ConfigureAwait(false);

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

        // URL-decode the full message first so attempts like https://e%78ample.com are
        // resolved before extraction. UnescapeDataString may throw on malformed input.
        string decodedContent;
        try
        {
            decodedContent = Uri.UnescapeDataString(messageContent);
        }
        catch
        {
            decodedContent = messageContent;
        }

        var domains = ExtractDomains(decodedContent);
        if (domains.Count == 0)
        {
            return false; // No URLs to filter
        }

        // If there's a blocklist, check if any domain is blocked
        if (config.BlockedDomains?.Length > 0)
        {
            var blockedSet = config.BlockedDomains.Select(NormalizeDomain).ToHashSet();
            if (domains.Any(d => blockedSet.Contains(d)))
            {
                return true; // Blocked domain found
            }
        }

        // If there's an allowlist, check if all domains are allowed
        if (config.AllowedDomains?.Length > 0)
        {
            var allowedSet = config.AllowedDomains.Select(NormalizeDomain).ToHashSet();
            if (domains.Any(d => !allowedSet.Contains(d)))
            {
                return true; // Non-allowed domain found
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts and normalizes domains from raw URLs and markdown link destinations.
    /// </summary>
    private static List<string> ExtractDomains(string content)
    {
        var domains = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match m in UrlRegex.Matches(content))
        {
            domains.Add(NormalizeDomain(m.Groups[1].Value));
        }

        foreach (Match m in MarkdownUrlRegex.Matches(content))
        {
            // Group 1 is the full URL (e.g. https://example.com/path); strip scheme + path
            // to get just the host so it can be normalized and compared to the blocklist.
            var full = m.Groups[1].Value;
            var schemeIdx = full.IndexOf("://", StringComparison.Ordinal);
            var host = schemeIdx >= 0 ? full[(schemeIdx + 3)..] : full;
            var slashIdx = host.IndexOf('/');
            if (slashIdx >= 0) host = host[..slashIdx];
            domains.Add(NormalizeDomain(host));
        }

        return domains.ToList();
    }

    /// <summary>
    /// Normalizes a domain for comparison: percent-decodes, converts Punycode to Unicode
    /// (so IDN homograph attempts hit the blocklist), and lowercases.
    /// </summary>
    private static string NormalizeDomain(string domain)
    {
        if (string.IsNullOrEmpty(domain)) return domain;

        // Decode percent-encoded characters first.
        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(domain);
        }
        catch
        {
            decoded = domain;
        }

        // Convert Punycode (xn--*) to its Unicode form so blocklist matches catch homograph attempts.
        try
        {
            var idn = new IdnMapping();
            decoded = idn.GetUnicode(decoded);
        }
        catch (ArgumentException) { /* leave as-is on invalid IDN */ }

        return decoded.ToLowerInvariant();
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
