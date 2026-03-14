namespace Xcord.Entities;

/// <summary>
/// Junction table for ServerMember-Group many-to-many relationship.
/// Represents group assignments to server members.
/// </summary>
public sealed class MemberGroup
{
    public long UserId { get; set; }
    public long ServerId { get; set; }
    public long GroupId { get; set; }

    // Navigation properties
    public ServerMember ServerMember { get; set; } = null!;
    public Group Group { get; set; } = null!;
}
