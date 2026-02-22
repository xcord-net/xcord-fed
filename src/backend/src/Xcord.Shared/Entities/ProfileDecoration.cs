using Xcord;

namespace Xcord.Entities;

public sealed class ProfileDecoration : ISoftDeletable
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string? BannerUrl { get; set; }
    public string? BannerColor { get; set; }
    public string? AvatarFrameUrl { get; set; }
    public string? ProfileEffect { get; set; }
    public string? Bio { get; set; }
    public string? Pronouns { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public User User { get; set; } = null!;
}
