using Xcord.Entities;

namespace Xcord.Features.Automod;

public sealed record AutomodRuleDto(
    long Id,
    long ServerId,
    string Name,
    bool Enabled,
    AutomodTriggerType TriggerType,
    string TriggerConfig,
    AutomodActionType ActionType,
    string? ActionConfig,
    string? ExemptRoleIds,
    string? ExemptChannelIds,
    bool ExemptBots,
    long? ChannelId,
    DateTimeOffset CreatedAt
);
