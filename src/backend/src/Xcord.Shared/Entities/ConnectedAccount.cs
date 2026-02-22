using Xcord;

namespace Xcord.Entities;

public sealed class ConnectedAccount : ISoftDeletable
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderAccountId { get; set; } = string.Empty;
    public string? ProviderUsername { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public bool IsVerified { get; set; }
    public bool ShowOnProfile { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public User User { get; set; } = null!;
}
