using Xcord;

namespace Xcord.Entities;

public sealed class OnboardingConfig : ISoftDeletable
{
    public long Id { get; set; }
    public long ServerId { get; set; }
    public bool IsEnabled { get; set; }
    public string? DefaultChannelIds { get; set; }
    public string? RulesText { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Server Server { get; set; } = null!;
    public ICollection<OnboardingPrompt> Prompts { get; set; } = new List<OnboardingPrompt>();
}
