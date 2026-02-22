using Xcord;

namespace Xcord.Entities;

public sealed class SoundboardSound : ISoftDeletable
{
    public long Id { get; set; }
    public long ServerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string S3Key { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public long UploadedByUserId { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Server Server { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}
