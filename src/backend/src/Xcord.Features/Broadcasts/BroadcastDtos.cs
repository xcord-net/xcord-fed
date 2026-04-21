namespace Xcord.Features.Broadcasts;

/// <summary>
/// Full representation of a broadcast, returned by Get/List endpoints and
/// serialized into SignalR events that require a complete snapshot.
/// </summary>
public sealed record BroadcastDto(
    long Id,
    long ChannelId,
    long HostUserId,
    string LayoutPreset,
    string Status,
    string HlsUrl,
    string RoomName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<BroadcastStageSlotDto> StageSlots,
    IReadOnlyList<BroadcastStreambotDto> Streambots);

/// <summary>
/// A single stage slot on a broadcast, carrying the occupying user and layout index.
/// </summary>
public sealed record BroadcastStageSlotDto(long UserId, int SlotIndex);

/// <summary>
/// Per-relay status for a streambot attached to a broadcast.
/// LastError is populated only when Status is Failed.
/// </summary>
public sealed record BroadcastStreambotDto(
    long StreambotId,
    string Name,
    string Status,
    string? LastError);
