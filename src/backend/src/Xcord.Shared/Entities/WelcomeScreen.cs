using Xcord;

namespace Xcord.Entities;

public sealed class WelcomeScreen : ISoftDeletable
{
    public long Id { get; set; }
    public long ServerId { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Server Server { get; set; } = null!;
    public ICollection<WelcomeScreenChannel> Channels { get; set; } = new List<WelcomeScreenChannel>();
}
