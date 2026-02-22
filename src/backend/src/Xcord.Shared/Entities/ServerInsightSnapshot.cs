using Xcord;

namespace Xcord.Entities;

public sealed class ServerInsightSnapshot : ISoftDeletable
{
    public long Id { get; set; }
    public long ServerId { get; set; }
    public DateOnly Date { get; set; }
    public int TotalMembers { get; set; }
    public int NewMembers { get; set; }
    public int MembersLeft { get; set; }
    public int MessageCount { get; set; }
    public int ActiveMembers { get; set; }
    public string? TopChannelIds { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Server Server { get; set; } = null!;
}
