namespace Xcord.Entities;

/// <summary>
/// Types of automod rule triggers.
/// </summary>
public enum AutomodTriggerType
{
    /// <summary>
    /// Keyword-based matching (check TriggerConfig.keywords array).
    /// </summary>
    Keyword = 0,

    /// <summary>
    /// Regex pattern matching (check TriggerConfig.pattern).
    /// </summary>
    Regex = 1,

    /// <summary>
    /// Mention spam detection (count mentions > TriggerConfig.maxMentions).
    /// </summary>
    MentionSpam = 2,

    /// <summary>
    /// Message spam detection (Redis rate limit check).
    /// </summary>
    MessageSpam = 3,

    /// <summary>
    /// Link filtering (check against allowed/blocked domains).
    /// </summary>
    LinkFilter = 4
}
