using Xcord;

namespace Xcord.Entities;

public sealed class UserActivity : ISoftDeletable
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public ActivityType ActivityType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? State { get; set; }
    public string? LargeImageUrl { get; set; }
    public string? SmallImageUrl { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public User User { get; set; } = null!;
}

public enum ActivityType
{
    Playing = 0,
    Streaming = 1,
    Listening = 2,
    Watching = 3,
    Custom = 4,
    Competing = 5
}
