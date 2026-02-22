namespace Xcord.Entities;

/// <summary>
/// Junction table linking forum posts (threads) to tags.
/// NOT soft-deleted.
/// </summary>
public sealed class ForumPostTag
{
    /// <summary>
    /// Thread ID (FK to Thread, part of composite PK).
    /// </summary>
    public long ThreadId { get; set; }

    /// <summary>
    /// Forum tag ID (FK to ForumTag, part of composite PK).
    /// </summary>
    public long ForumTagId { get; set; }

    // Navigation properties
    public Thread Thread { get; set; } = null!;
    public ForumTag ForumTag { get; set; } = null!;
}
