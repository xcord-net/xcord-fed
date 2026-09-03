using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Broadcasts;

/// <summary>
/// Shared helper used by the broadcast mutation handlers to construct LiveKit
/// RoomCompositeEgress outputs (HLS + RTMP) and the template URL rendered by the
/// egress compositor's headless browser.
/// </summary>
public sealed class BroadcastEgressBuilder
{
    private const int HlsSegmentDurationSeconds = 4;
    private const string HlsBucket = "broadcasts";
    private const string S3Region = "us-east-1";
    private const string EgressIdentityPrefix = "egress-";

    private readonly AppDbContext _db;
    private readonly ILiveKitService _livekit;
    private readonly IEncryptionService _encryption;
    private readonly LiveKitOptions _livekitOptions;
    private readonly StorageOptions _storageOptions;
    private readonly InstanceOptions _instanceOptions;

    public BroadcastEgressBuilder(
        AppDbContext db,
        ILiveKitService livekit,
        IEncryptionService encryption,
        IOptions<LiveKitOptions> livekitOptions,
        IOptions<StorageOptions> storageOptions,
        IOptions<InstanceOptions> instanceOptions)
    {
        _db = db;
        _livekit = livekit;
        _encryption = encryption;
        _livekitOptions = livekitOptions.Value;
        _storageOptions = storageOptions.Value;
        _instanceOptions = instanceOptions.Value;
    }

    /// <summary>
    /// Builds the canonical LiveKit room name for a broadcast.
    /// Namespaced by instance domain to prevent collision on shared LiveKit deployments.
    /// </summary>
    public string BuildRoomName(long channelId) =>
        $"{_instanceOptions.Domain}:broadcast:{channelId}";

    /// <summary>
    /// Returns the public HLS playlist URL served by Caddy in front of MinIO.
    /// </summary>
    public string BuildHlsUrl(long broadcastId)
    {
        var baseUrl = (_livekitOptions.HlsBaseUrl ?? string.Empty).TrimEnd('/');
        if (baseUrl.Length == 0)
            throw new InvalidOperationException("LiveKitOptions.HlsBaseUrl is not configured.");
        return $"{baseUrl}/{HlsBucket}/{broadcastId}/playlist.m3u8";
    }

    /// <summary>
    /// Builds the template URL passed to LiveKit's egress compositor. The compositor
    /// fetches this URL in a headless browser, which then joins the LiveKit room
    /// as a read-only participant and lays out subscribed tracks per the preset.
    /// </summary>
    public string BuildTemplateUrl(
        long broadcastId,
        BroadcastLayoutPreset preset,
        IEnumerable<BroadcastStageSlot> slots,
        string roomName)
    {
        var baseUrl = (_livekitOptions.EgressTemplateBaseUrl ?? string.Empty).TrimEnd('/');
        if (baseUrl.Length == 0)
            throw new InvalidOperationException("LiveKitOptions.EgressTemplateBaseUrl is not configured.");

        var slotsPayload = slots
            .OrderBy(s => s.SlotIndex)
            .Select(s => new { userId = s.UserId.ToString(), slotIndex = s.SlotIndex })
            .ToArray();
        var slotsJson = JsonSerializer.Serialize(slotsPayload);

        // Dedicated egress identity scoped to this broadcast. 24-hour TTL so long
        // broadcasts don't require re-issuing tokens mid-stream. canPublish=false
        // ensures the compositor can only subscribe to the stage participants.
        var egressIdentity = EgressIdentityToUserId(broadcastId);
        var egressToken = _livekit.GenerateToken(
            userId: egressIdentity,
            roomName: roomName,
            canPublish: false,
            canSubscribe: true,
            canPublishData: false,
            canScreenShare: false,
            ttl: TimeSpan.FromHours(24));

        var presetSlug = PresetSlug(preset);
        return
            $"{baseUrl}/api/v1/broadcast-layout/{presetSlug}" +
            $"?room={Uri.EscapeDataString(roomName)}" +
            $"&token={Uri.EscapeDataString(egressToken)}" +
            $"&slots={Uri.EscapeDataString(slotsJson)}";
    }

    /// <summary>
    /// Tells the running compositor what the stage looks like now.
    /// </summary>
    /// <remarks>
    /// This is the reason a stage or layout change no longer interrupts the
    /// stream. The compositor is already connected and rendering; it needs the
    /// new arrangement, not a new page. The whole state goes over the wire each
    /// time so a missed message cannot leave the render permanently wrong.
    /// </remarks>
    public Task PublishLayoutAsync(
        long channelId,
        BroadcastLayoutPreset preset,
        IEnumerable<BroadcastStageSlot> slots,
        CancellationToken ct)
    {
        var state = new BroadcastControl.LayoutState(
            PresetSlug(preset),
            slots
                .OrderBy(s => s.SlotIndex)
                .Select(s => new BroadcastControl.SlotAssignment(s.UserId.ToString(), s.SlotIndex))
                .ToArray());

        return _livekit.SendDataAsync(
            BuildRoomName(channelId),
            BroadcastControl.Topic,
            BroadcastControl.Serialize(state),
            ct);
    }

    /// <summary>
    /// Builds the full set of egress outputs for a broadcast: one HLS segment output
    /// (S3/MinIO) plus one RTMP stream output per active streambot. Stream keys are
    /// decrypted at call time and never retained after the outputs are returned.
    /// </summary>
    public async Task<List<EgressOutput>> BuildOutputsAsync(
        long broadcastId,
        CancellationToken ct)
    {
        var outputs = new List<EgressOutput>
        {
            new HlsEgressOutput(
                PlaylistName: "playlist.m3u8",
                SegmentPrefix: $"{broadcastId}/segment",
                SegmentDurationSeconds: HlsSegmentDurationSeconds,
                Bucket: HlsBucket,
                Region: S3Region,
                AccessKey: RequireStorageValue(_storageOptions.AccessKey, "Storage.AccessKey"),
                AccessSecret: RequireStorageValue(_storageOptions.SecretKey, "Storage.SecretKey"),
                Endpoint: RequireStorageValue(_storageOptions.Endpoint, "Storage.Endpoint"),
                ForcePathStyle: true)
        };

        // Include every streambot that is currently expected to be receiving the feed.
        // Ended/Failed bots are excluded because their RTMP destinations are no longer valid.
        var activeBots = await _db.BroadcastStreambots
            .Where(bs => bs.BroadcastId == broadcastId
                && bs.Status != BroadcastStreambotStatus.Ended
                && bs.Status != BroadcastStreambotStatus.Failed)
            .Include(bs => bs.StreamBot)
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var bs in activeBots)
        {
            if (bs.StreamBot == null || bs.StreamBot.EncryptedStreamKey.Length == 0)
                continue;

            var streamKey = _encryption.Decrypt(bs.StreamBot.EncryptedStreamKey);
            var fullUrl = $"{bs.StreamBot.RtmpUrl.TrimEnd('/')}/{streamKey}";
            outputs.Add(new RtmpEgressOutput(
                Url: fullUrl,
                VideoBitrateKbps: null,
                Width: null,
                Height: null));
        }

        return outputs;
    }

    public static string PresetSlug(BroadcastLayoutPreset preset) => preset switch
    {
        BroadcastLayoutPreset.Grid => "grid",
        BroadcastLayoutPreset.Spotlight => "spotlight",
        BroadcastLayoutPreset.Pip => "pip",
        BroadcastLayoutPreset.SideBySide => "side-by-side",
        BroadcastLayoutPreset.AudioShow => "audio-show",
        _ => "grid"
    };

    // LiveKit participant identity is a string but our ILiveKitService accepts a long.
    // Use a deterministic negative-namespace synthetic id so the egress identity can't
    // collide with any real user id (which are always positive snowflakes).
    private static long EgressIdentityToUserId(long broadcastId) =>
        -broadcastId;

    private static string RequireStorageValue(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{name} is not configured.");
        return value;
    }
}
