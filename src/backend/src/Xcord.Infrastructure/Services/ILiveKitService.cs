namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for interacting with LiveKit voice infrastructure.
/// </summary>
public interface ILiveKitService
{
    /// <summary>
    /// Generates a JWT token for LiveKit room access with optional server-enforced quality constraints.
    /// Quality constraints are embedded into the token's video grant so the LiveKit media server
    /// enforces bitrate and resolution limits rather than relying solely on client-side hints.
    /// </summary>
    /// <param name="userId">User identifier (becomes participant identity)</param>
    /// <param name="roomName">LiveKit room name</param>
    /// <param name="canPublish">Can publish audio/video tracks</param>
    /// <param name="canSubscribe">Can subscribe to other participants' tracks</param>
    /// <param name="canPublishData">Can publish data messages</param>
    /// <param name="canScreenShare">Can share screen</param>
    /// <param name="ttl">Token time-to-live</param>
    /// <param name="qualityConstraints">
    /// Optional tier-based quality constraints to embed in the token.
    /// When null, no server-side quality limits are applied.
    /// </param>
    /// <returns>JWT token for LiveKit client</returns>
    string GenerateToken(
        long userId,
        string roomName,
        bool canPublish,
        bool canSubscribe,
        bool canPublishData,
        bool canScreenShare,
        TimeSpan ttl,
        VideoQualityConstraints? qualityConstraints = null);

    /// <summary>
    /// Removes a participant from a LiveKit room.
    /// </summary>
    /// <param name="roomName">LiveKit room name</param>
    /// <param name="participantIdentity">Participant identity (userId)</param>
    Task RemoveParticipantAsync(string roomName, string participantIdentity);

    /// <summary>
    /// Publishes a reliable data message to every participant in a room, including the
    /// headless browser that renders a broadcast's layout.
    /// </summary>
    /// <remarks>
    /// This is how a broadcast's stage and layout change without interrupting the stream.
    /// Restarting the egress to pick up a new stage would tear down and re-establish every
    /// RTMP push, which viewers on the downstream platforms see as the stream dropping.
    /// </remarks>
    /// <param name="roomName">LiveKit room name.</param>
    /// <param name="topic">Topic the receiver filters on.</param>
    /// <param name="payload">UTF-8 message body.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendDataAsync(string roomName, string topic, string payload, CancellationToken ct);

    /// <summary>
    /// Starts a RoomCompositeEgress that renders <paramref name="templateUrl"/> and publishes
    /// the composited stream to the supplied <paramref name="outputs"/> (HLS segments,
    /// RTMP destinations, or both).
    /// </summary>
    /// <param name="roomName">LiveKit room name to compose.</param>
    /// <param name="templateUrl">Absolute URL of the HTML template the egress will render.</param>
    /// <param name="outputs">One or more <see cref="EgressOutput"/> destinations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>LiveKit egress identifier (<c>egress_id</c>).</returns>
    Task<string> StartRoomCompositeEgressAsync(
        string roomName,
        string templateUrl,
        IEnumerable<EgressOutput> outputs,
        CancellationToken ct);

    /// <summary>
    /// Stops an in-progress egress by id. No-op if the egress has already completed server-side.
    /// </summary>
    /// <param name="egressId">Identifier returned by <see cref="StartRoomCompositeEgressAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task StopEgressAsync(string egressId, CancellationToken ct);

    /// <summary>
    /// Stops an existing egress and starts a new RoomCompositeEgress with refreshed outputs.
    /// Errors from the stop phase are swallowed (the prior egress may already be gone).
    /// </summary>
    /// <param name="oldEgressId">Identifier of the egress being replaced.</param>
    /// <param name="roomName">LiveKit room name to compose.</param>
    /// <param name="templateUrl">Absolute URL of the HTML template the egress will render.</param>
    /// <param name="outputs">New set of egress outputs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>New egress identifier.</returns>
    Task<string> RestartEgressWithNewOutputsAsync(
        string oldEgressId,
        string roomName,
        string templateUrl,
        IEnumerable<EgressOutput> outputs,
        CancellationToken ct);
}
