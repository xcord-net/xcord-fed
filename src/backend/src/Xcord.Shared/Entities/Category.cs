using Xcord;

namespace Xcord.Entities;

/// <summary>
/// Represents a channel category in a server.
/// Categories group channels and can be collapsed/expanded in the UI.
/// </summary>
public sealed class Category : ISoftDeletable
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
    /// Category name (max 100 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Position for ordering (lower = higher up).
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Category creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Server Server { get; set; } = null!;
    public ICollection<Channel> Channels { get; set; } = new List<Channel>();
}
