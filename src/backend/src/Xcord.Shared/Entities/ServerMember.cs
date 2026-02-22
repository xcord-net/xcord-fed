namespace Xcord.Entities;

/// <summary>
/// Represents a server membership (junction table with composite PK).
/// Includes per-server nickname and avatar override.
/// </summary>
public sealed class ServerMember : ISoftDeletable
{
    /// <summary>
    /// User ID (part of composite PK, FK to User).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Server ID (part of composite PK, FK to Server).
    /// </summary>
    public long ServerId { get; set; }

    /// <summary>
    /// Per-server nickname override (max 32 characters).
    /// </summary>
    public string? Nickname { get; set; }

    /// <summary>
    /// Per-server avatar URL override (max 512 characters).
    /// </summary>
    public string? ServerAvatarUrl { get; set; }

    /// <summary>
    /// Timestamp when the user joined the server.
    /// </summary>
    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Server Server { get; set; } = null!;
    public ICollection<MemberRole> MemberRoles { get; set; } = new List<MemberRole>();
}
