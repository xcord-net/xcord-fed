using Xcord;

namespace Xcord.Entities;

public sealed class AppReview : ISoftDeletable
{
    public long Id { get; set; }
    public long AppListingId { get; set; }
    public long UserId { get; set; }
    public int Rating { get; set; }
    public string? Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public AppListing AppListing { get; set; } = null!;
    public User User { get; set; } = null!;
}
