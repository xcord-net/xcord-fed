namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for interacting with LiveKit voice infrastructure.
/// </summary>
public interface ILiveKitService
{
    /// <summary>
    /// Generates a JWT token for LiveKit room access.
    /// </summary>
    /// <param name="userId">User identifier (becomes participant identity)</param>
    /// <param name="roomName">LiveKit room name</param>
    /// <param name="canPublish">Can publish audio/video tracks</param>
    /// <param name="canSubscribe">Can subscribe to other participants' tracks</param>
    /// <param name="canPublishData">Can publish data messages</param>
    /// <param name="canScreenShare">Can share screen</param>
    /// <param name="ttl">Token time-to-live</param>
    /// <returns>JWT token for LiveKit client</returns>
    string GenerateToken(
        long userId,
        string roomName,
        bool canPublish,
        bool canSubscribe,
        bool canPublishData,
        bool canScreenShare,
        TimeSpan ttl);

    /// <summary>
    /// Removes a participant from a LiveKit room.
    /// </summary>
    /// <param name="roomName">LiveKit room name</param>
    /// <param name="participantIdentity">Participant identity (userId)</param>
    Task RemoveParticipantAsync(string roomName, string participantIdentity);
}
