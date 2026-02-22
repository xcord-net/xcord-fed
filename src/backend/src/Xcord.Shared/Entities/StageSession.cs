using Xcord;

namespace Xcord.Entities;

public sealed class StageSession : ISoftDeletable
{
    public long Id { get; set; }
    public long ChannelId { get; set; }
    public string? Topic { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Channel Channel { get; set; } = null!;
    public ICollection<StageSpeaker> Speakers { get; set; } = new List<StageSpeaker>();
}
