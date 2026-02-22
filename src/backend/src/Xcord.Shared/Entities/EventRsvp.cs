namespace Xcord.Entities;

/// <summary>
/// Represents a user's RSVP to a scheduled event.
/// This is a many-to-many relationship between users and events.
/// NOT soft-deleted - RSVPs are hard-deleted when removed.
/// </summary>
public sealed class EventRsvp
{
    /// <summary>
    /// Event ID (composite PK, FK to ScheduledEvent, Cascade delete).
    /// </summary>
    public long EventId { get; set; }

    /// <summary>
    /// User ID (composite PK, FK to User, Cascade delete).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// RSVP creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public ScheduledEvent ScheduledEvent { get; set; } = null!;
    public User User { get; set; } = null!;
}
