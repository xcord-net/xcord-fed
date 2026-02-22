namespace Xcord.Entities;

/// <summary>
/// Junction table for ServerMember-Role many-to-many relationship.
/// Represents role assignments to server members.
/// </summary>
public sealed class MemberRole
{
    /// <summary>
    /// User ID (part of composite PK, FK to ServerMember).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Server ID (part of composite PK, FK to ServerMember).
    /// </summary>
    public long ServerId { get; set; }

    /// <summary>
    /// Role ID (part of composite PK, FK to Role).
    /// </summary>
    public long RoleId { get; set; }

    // Navigation properties
    public ServerMember ServerMember { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
