namespace Xcord.Infrastructure.Services;

/// <summary>
/// Quality constraints derived from TierOptions that are embedded into LiveKit JWT tokens
/// so the server enforces limits at the media-server level, not just as client-side hints.
/// A value of 0 for any bitrate/dimension field means "no limit imposed".
/// </summary>
public sealed record VideoQualityConstraints
{
    /// <summary>Maximum audio publish bitrate in kbps. 0 = unlimited.</summary>
    public int MaxAudioBitrateKbps { get; init; }

    /// <summary>Maximum camera video publish bitrate in kbps. 0 = unlimited.</summary>
    public int MaxVideoBitrateKbps { get; init; }

    /// <summary>Maximum camera video width in pixels. 0 = unlimited.</summary>
    public int MaxVideoWidth { get; init; }

    /// <summary>Maximum camera video height in pixels. 0 = unlimited.</summary>
    public int MaxVideoHeight { get; init; }

    /// <summary>Maximum camera video frame rate. 0 = unlimited.</summary>
    public int MaxVideoFps { get; init; }

    /// <summary>Maximum screen-share bitrate in kbps. 0 = unlimited.</summary>
    public int MaxScreenShareBitrateKbps { get; init; }

    /// <summary>
    /// Whether simulcast is enabled for this tier.
    /// When false, only a single quality layer is published (standard tier).
    /// When true, multiple layers are published for adaptive quality (HD tier).
    /// </summary>
    public bool EnableSimulcast { get; init; }
}
