using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xcord.Features.Broadcasts;

/// <summary>
/// The control channel between the API and the headless browser that composites
/// a broadcast.
/// </summary>
/// <remarks>
/// Stage assignments and the layout preset used to be query parameters on the
/// egress template URL, which meant the only way to change either was to stop
/// the egress and start a new one. That tore down every RTMP push in the
/// process, so adding one person to the stage dropped the stream on YouTube,
/// Twitch and everywhere else it was being relayed.
///
/// They travel over a LiveKit data message instead. The composite page stays
/// connected and re-arranges itself, and the egress - and therefore every
/// downstream connection - is left alone. An egress restart is now reserved for
/// the one case that genuinely changes its outputs: the set of destinations.
/// </remarks>
public static class BroadcastControl
{
    /// <summary>Topic the composite page filters incoming data messages on.</summary>
    public const string Topic = "xcord.broadcast.control";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// A complete description of what the composite should show. Sent whole
    /// rather than as a delta so a dropped message cannot leave the rendered
    /// stage permanently out of step with the truth.
    /// </summary>
    /// <param name="Preset">Layout preset name, lowercase and hyphenated.</param>
    /// <param name="Slots">Every occupied stage slot.</param>
    public sealed record LayoutState(string Preset, IReadOnlyList<SlotAssignment> Slots);

    /// <param name="UserId">Participant identity, as a string - snowflakes do not
    /// survive a round trip through JavaScript numbers.</param>
    /// <param name="SlotIndex">Zero-based position in the preset's layout.</param>
    public sealed record SlotAssignment(string UserId, int SlotIndex);

    public static string Serialize(LayoutState state) =>
        JsonSerializer.Serialize(state, SerializerOptions);
}
