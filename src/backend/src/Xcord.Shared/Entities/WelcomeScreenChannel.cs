using Xcord;

namespace Xcord.Entities;

public sealed class WelcomeScreenChannel : ISoftDeletable
{
    public long Id { get; set; }
    public long WelcomeScreenId { get; set; }
    public long ChannelId { get; set; }
    public string? Description { get; set; }
    public string? EmojiName { get; set; }
    public int Position { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public WelcomeScreen WelcomeScreen { get; set; } = null!;
    public Channel Channel { get; set; } = null!;
}
