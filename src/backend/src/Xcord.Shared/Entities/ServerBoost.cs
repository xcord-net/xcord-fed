using Xcord;

namespace Xcord.Entities;

public sealed class ServerBoost : ISoftDeletable
{
    public long Id { get; set; }
    public long ServerId { get; set; }
    public long UserId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Server Server { get; set; } = null!;
    public User User { get; set; } = null!;
}
