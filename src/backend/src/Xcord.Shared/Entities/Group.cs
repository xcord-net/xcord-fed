using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a group in a server.
/// Groups define role sets that can be assigned to members.
/// Every server has an @everyone group (IsEveryone=true) that applies to all members.
/// </summary>
public sealed class Group : ISoftDeletable
{
    public long Id { get; set; }
    public long ServerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public long Roles { get; set; }
    public int Position { get; set; }
    public bool IsEveryone { get; set; }
    public string? LimitsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Server Server { get; set; } = null!;
    public ICollection<MemberGroup> MemberGroups { get; set; } = new List<MemberGroup>();
}
