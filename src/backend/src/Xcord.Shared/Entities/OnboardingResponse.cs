namespace Xcord.Entities;

public sealed class OnboardingCompletion : ISoftDeletable
{
    public long Id { get; set; }
    public long ServerId { get; set; }
    public long UserId { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string? ResponseDataJson { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Server Server { get; set; } = null!;
    public User User { get; set; } = null!;
}
