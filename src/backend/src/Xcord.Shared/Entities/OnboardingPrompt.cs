using Xcord;

namespace Xcord.Entities;

public sealed class OnboardingPrompt : ISoftDeletable
{
    public long Id { get; set; }
    public long OnboardingConfigId { get; set; }
    public string Title { get; set; } = string.Empty;
    public OnboardingPromptType Type { get; set; }
    public bool IsRequired { get; set; }
    public int Position { get; set; }
    public string OptionsJson { get; set; } = "[]";
    public DateTimeOffset? DeletedAt { get; set; }

    public OnboardingConfig OnboardingConfig { get; set; } = null!;
}

public enum OnboardingPromptType
{
    MultipleChoice = 0,
    RoleSelect = 1,
    ChannelSelect = 2,
    RulesAgreement = 3
}
