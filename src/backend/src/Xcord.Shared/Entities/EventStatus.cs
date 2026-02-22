namespace Xcord.Entities;

/// <summary>
/// Status of a scheduled event.
/// </summary>
public enum EventStatus
{
    /// <summary>
    /// Event is scheduled but has not started yet.
    /// </summary>
    Scheduled = 0,

    /// <summary>
    /// Event is currently active (in progress).
    /// </summary>
    Active = 1,

    /// <summary>
    /// Event has completed.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Event has been cancelled.
    /// </summary>
    Cancelled = 3
}
