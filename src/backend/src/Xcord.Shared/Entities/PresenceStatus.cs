namespace Xcord.Entities;

/// <summary>
/// User presence status.
/// </summary>
public enum PresenceStatus
{
    /// <summary>
    /// User is online and available.
    /// </summary>
    Online = 0,

    /// <summary>
    /// User is away from keyboard.
    /// </summary>
    Away = 1,

    /// <summary>
    /// User is in Do Not Disturb mode.
    /// </summary>
    DND = 2,

    /// <summary>
    /// User is offline or invisible.
    /// </summary>
    Offline = 3
}
