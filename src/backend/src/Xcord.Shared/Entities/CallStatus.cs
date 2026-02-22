namespace Xcord.Entities;

/// <summary>
/// Status of a direct call (1:1 voice/video).
/// </summary>
public enum CallStatus
{
    /// <summary>
    /// Call is ringing, waiting for recipient to answer.
    /// </summary>
    Ringing = 0,

    /// <summary>
    /// Call has been answered and is active.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Call has ended normally.
    /// </summary>
    Ended = 2,

    /// <summary>
    /// Call was declined by recipient.
    /// </summary>
    Declined = 3,

    /// <summary>
    /// Call was not answered (timeout after 30 seconds).
    /// </summary>
    Missed = 4
}
