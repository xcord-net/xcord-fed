using Xcord.Entities;

namespace Xcord.Entities;

/// <summary>
/// Represents an automod rule for a server.
/// Rules define triggers (conditions to check) and actions (what to do when triggered).
/// </summary>
public sealed class AutomodRule : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Server ID (FK to Server, Cascade delete).
    /// </summary>
    public long ServerId { get; set; }

    /// <summary>
    /// Rule name (max 100 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this rule is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The type of trigger to check.
    /// </summary>
    public AutomodTriggerType TriggerType { get; set; }

    /// <summary>
    /// Trigger-specific configuration stored as jsonb.
    /// Structure depends on TriggerType:
    /// - Keyword: { keywords: string[], matchWholeWord: bool }
    /// - Regex: { pattern: string, caseSensitive: bool }
    /// - MentionSpam: { maxMentions: int }
    /// - MessageSpam: { maxMessages: int, intervalSeconds: int }
    /// - LinkFilter: { allowedDomains: string[], blockedDomains: string[] }
    /// </summary>
    public string TriggerConfig { get; set; } = string.Empty;

    /// <summary>
    /// The action to take when the rule is triggered.
    /// </summary>
    public AutomodActionType ActionType { get; set; }

    /// <summary>
    /// Action-specific configuration stored as jsonb.
    /// Structure depends on ActionType:
    /// - TimeoutUser: { timeoutDurationMinutes: int }
    /// - AlertMods: { alertChannelId: long }
    /// - DeleteMessage/BlockMessage: {} (no config needed)
    /// </summary>
    public string? ActionConfig { get; set; }

    /// <summary>
    /// Comma-separated role IDs that are exempt from this rule.
    /// </summary>
    public string? ExemptRoleIds { get; set; }

    /// <summary>
    /// Comma-separated channel IDs that are exempt from this rule.
    /// </summary>
    public string? ExemptChannelIds { get; set; }

    /// <summary>
    /// Whether bots are exempt from this rule.
    /// </summary>
    public bool ExemptBots { get; set; } = true;

    /// <summary>
    /// Rule creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Server Server { get; set; } = null!;
}
