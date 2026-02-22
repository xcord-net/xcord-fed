namespace Xcord.Entities;

/// <summary>
/// Represents a user's membership in a thread.
/// Used to track which users have joined a thread.
/// </summary>
public sealed class ThreadMember
{
    /// <summary>
    /// User ID (part of composite PK, FK to User).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Thread ID (part of composite PK, FK to Thread).
    /// </summary>
    public long ThreadId { get; set; }

    /// <summary>
    /// When the user joined this thread.
    /// </summary>
    public DateTimeOffset JoinedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Thread Thread { get; set; } = null!;
}
