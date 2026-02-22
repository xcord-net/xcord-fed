using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a role in a server.
/// Roles define permission sets that can be assigned to members.
/// Every server has an @everyone role (IsEveryone=true) that applies to all members.
/// </summary>
public sealed class Role : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Server ID (FK to Server, Cascade delete).
    /// </summary>
    public long ServerId { get; set; }

    /// <summary>
    /// Role name (max 100 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Role color in hex format (e.g., "#FF5733", max 7 characters).
    /// Null means no color.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Permission bitfield (64-bit).
    /// </summary>
    public long Permissions { get; set; }

    /// <summary>
    /// Role position for hierarchy (higher = more important).
    /// Used for permission override precedence and display order.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// True if this is the @everyone role (one per server).
    /// The @everyone role cannot be deleted and applies to all members.
    /// </summary>
    public bool IsEveryone { get; set; }

    /// <summary>
    /// Role creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Server Server { get; set; } = null!;
    public ICollection<MemberRole> MemberRoles { get; set; } = new List<MemberRole>();
}
