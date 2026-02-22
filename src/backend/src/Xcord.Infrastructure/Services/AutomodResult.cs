using Xcord.Entities;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Result of automod evaluation for a message.
/// </summary>
public sealed class AutomodResult
{
    /// <summary>
    /// Whether the message is allowed to be sent.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// The reason the message was blocked (if applicable).
    /// </summary>
    public string? BlockReason { get; init; }

    /// <summary>
    /// Actions to execute after the message is persisted (if any).
    /// </summary>
    public List<AutomodDeferredAction> DeferredActions { get; init; } = new();

    public static AutomodResult Allowed() => new() { IsAllowed = true };

    public static AutomodResult Blocked(string reason) => new()
    {
        IsAllowed = false,
        BlockReason = reason
    };

    public static AutomodResult WithActions(List<AutomodDeferredAction> actions) => new()
    {
        IsAllowed = true,
        DeferredActions = actions
    };
}

/// <summary>
/// Represents an automod action to execute after message persistence.
/// </summary>
public sealed class AutomodDeferredAction
{
    public AutomodActionType ActionType { get; init; }
    public long RuleId { get; init; }
    public string RuleName { get; init; } = string.Empty;
    public Dictionary<string, object>? Config { get; init; }
}
