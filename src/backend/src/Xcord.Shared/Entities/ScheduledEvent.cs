using Xcord.Entities;

namespace Xcord.Entities;

/// <summary>
/// Represents a scheduled event in a server.
/// Events can be associated with a channel (voice channel events) or external (location-based).
/// </summary>
public sealed class ScheduledEvent : ISoftDeletable
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
    /// Creator user ID (FK to User, Restrict delete).
    /// </summary>
    public long CreatorId { get; set; }

    /// <summary>
    /// Channel ID (FK to Channel, SetNull on delete).
    /// Null for external events.
    /// </summary>
    public long? ChannelId { get; set; }

    /// <summary>
    /// Event name (max 100 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Event description (max 1000 characters).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// External location for events not tied to a channel (max 100 characters).
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Event cover image URL (max 512 characters).
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Scheduled start time.
    /// </summary>
    public DateTimeOffset ScheduledStartTime { get; set; }

    /// <summary>
    /// Scheduled end time (optional).
    /// </summary>
    public DateTimeOffset? ScheduledEndTime { get; set; }

    /// <summary>
    /// Current event status.
    /// </summary>
    public EventStatus Status { get; set; } = EventStatus.Scheduled;

    /// <summary>
    /// Denormalized RSVP count (number of users interested).
    /// </summary>
    public int InterestedCount { get; set; } = 0;

    /// <summary>
    /// Whether notification has been sent for this event (15 minutes before start).
    /// </summary>
    public bool NotificationSent { get; set; } = false;

    /// <summary>
    /// Event creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Server Server { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public Channel? Channel { get; set; }
}
