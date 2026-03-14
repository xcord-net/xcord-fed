using Xcord;

namespace Xcord.Entities;

public sealed class ServerTemplate : ISoftDeletable
{
    public long Id { get; set; }
    public long? SourceServerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ChannelData { get; set; } = "[]";
    public string GroupData { get; set; } = "[]";
    public int UsageCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Server? SourceServer { get; set; }
}
