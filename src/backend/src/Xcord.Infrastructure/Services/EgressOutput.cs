namespace Xcord.Infrastructure.Services;

/// <summary>
/// Abstract base type for egress destination configurations.
/// Concrete implementations describe HLS (segment) or RTMP (stream) outputs
/// that LiveKit's Egress service will write to when a composite egress runs.
/// </summary>
public abstract record EgressOutput;

/// <summary>
/// HLS segment output backed by an S3-compatible bucket (MinIO in the typical xcord deployment).
/// </summary>
/// <param name="PlaylistName">Playlist file name (e.g. "playlist.m3u8").</param>
/// <param name="SegmentPrefix">Prefix applied to each generated segment (e.g. "segment").</param>
/// <param name="SegmentDurationSeconds">Segment duration in seconds (default 4).</param>
/// <param name="Bucket">Destination bucket name.</param>
/// <param name="Region">Bucket region (MinIO typically uses "us-east-1").</param>
/// <param name="AccessKey">S3 access key.</param>
/// <param name="AccessSecret">S3 secret key.</param>
/// <param name="Endpoint">S3 endpoint (e.g. http://minio:9000 for MinIO).</param>
/// <param name="ForcePathStyle">Whether to use path-style addressing (required for MinIO).</param>
public sealed record HlsEgressOutput(
    string PlaylistName,
    string SegmentPrefix,
    int SegmentDurationSeconds,
    string Bucket,
    string Region,
    string AccessKey,
    string AccessSecret,
    string Endpoint,
    bool ForcePathStyle = true
) : EgressOutput;

/// <summary>
/// RTMP push output (e.g. YouTube Live, Twitch, generic RTMP ingest).
/// </summary>
/// <param name="Url">Full RTMP URL including the stream key.</param>
/// <param name="VideoBitrateKbps">Optional bitrate override (kbps). Null uses egress defaults.</param>
/// <param name="Width">Optional width override. Null uses egress defaults.</param>
/// <param name="Height">Optional height override. Null uses egress defaults.</param>
public sealed record RtmpEgressOutput(
    string Url,
    int? VideoBitrateKbps,
    int? Width,
    int? Height
) : EgressOutput;
