using Xcord;

namespace Xcord.Entities;

public sealed class StageSpeaker : ISoftDeletable
{
    public long Id { get; set; }
    public long StageSessionId { get; set; }
    public long UserId { get; set; }
    public StageRole Role { get; set; }
    public DateTimeOffset? RequestedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public StageSession StageSession { get; set; } = null!;
    public User User { get; set; } = null!;
}

public enum StageRole
{
    Audience = 0,
    Speaker = 1,
    Invited = 2
}
