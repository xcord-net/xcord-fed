using Xcord;

namespace Xcord.Entities;

public sealed class AppListing : ISoftDeletable
{
    public long Id { get; set; }
    public long BotTokenId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? IconUrl { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public int InstallCount { get; set; }
    public bool IsVerified { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public BotToken BotToken { get; set; } = null!;
    public ICollection<AppReview> Reviews { get; set; } = new List<AppReview>();
}
