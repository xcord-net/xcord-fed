using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service implementation for LiveKit voice infrastructure.
/// Generates JWT tokens and manages room participants via LiveKit API.
/// </summary>
public sealed class LiveKitService : ILiveKitService
{
    private readonly LiveKitOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LiveKitService> _logger;

    private static readonly JsonSerializerOptions EgressJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public LiveKitService(
        IOptions<LiveKitOptions> options,
        HttpClient httpClient,
        ILogger<LiveKitService> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Builds and signs a LiveKit access token.
    /// </summary>
    /// <remarks>
    /// Every LiveKit token goes through here, because the ways to get one wrong
    /// are invisible: the `video` grant must be a JSON *object*, not a quoted
    /// string, and `nbf`/`exp` must be numeric dates - a string claim in either
    /// place is answered only with "invalid authorization token". Three separate
    /// builders each made those choices independently, and two of them made them
    /// wrongly.
    /// </remarks>
    private string BuildAccessToken(
        IDictionary<string, object> videoGrant,
        TimeSpan ttl,
        long? subject = null)
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = now.Add(ttl);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("video", JsonSerializer.Serialize(videoGrant), JsonClaimValueTypes.Json),
        };
        if (subject.HasValue)
        {
            claims.Insert(0, new Claim(JwtRegisteredClaimNames.Sub, subject.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Issuer, notBefore and expires come from the token itself so they are
        // written as the types the spec requires.
        var token = new JwtSecurityToken(
            issuer: _options.ApiKey,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiry.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateToken(
        long userId,
        string roomName,
        bool canPublish,
        bool canSubscribe,
        bool canPublishData,
        bool canScreenShare,
        TimeSpan ttl,
        VideoQualityConstraints? qualityConstraints = null)
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = now.Add(ttl);

        // Build LiveKit video grant
        var videoGrant = new Dictionary<string, object>
        {
            { "room", roomName },
            { "roomJoin", true },
            { "canPublish", canPublish },
            { "canSubscribe", canSubscribe },
            { "canPublishData", canPublishData }
        };

        // Add screen share capability if permitted
        if (canScreenShare)
        {
            videoGrant["canPublishSources"] = new[] { "camera", "microphone", "screen_share" };
        }

        // Embed server-enforced quality constraints into the token when provided.
        // LiveKit reads videoEncoding, audioEncoding, and screenShareEncoding from the
        // video grant at the time of publish and applies them as hard limits server-side.
        // Fields with value 0 are omitted (LiveKit interprets absence as "no limit").
        if (qualityConstraints != null)
        {
            // Audio encoding constraint
            if (qualityConstraints.MaxAudioBitrateKbps > 0)
            {
                videoGrant["audioEncoding"] = new Dictionary<string, object>
                {
                    { "maxBitrate", qualityConstraints.MaxAudioBitrateKbps * 1000 }
                };
            }

            // Camera video encoding constraints
            if (qualityConstraints.MaxVideoBitrateKbps > 0
                || qualityConstraints.MaxVideoWidth > 0
                || qualityConstraints.MaxVideoHeight > 0
                || qualityConstraints.MaxVideoFps > 0)
            {
                var videoEncoding = new Dictionary<string, object>();
                if (qualityConstraints.MaxVideoBitrateKbps > 0)
                    videoEncoding["maxBitrate"] = qualityConstraints.MaxVideoBitrateKbps * 1000;
                if (qualityConstraints.MaxVideoWidth > 0)
                    videoEncoding["maxWidth"] = qualityConstraints.MaxVideoWidth;
                if (qualityConstraints.MaxVideoHeight > 0)
                    videoEncoding["maxHeight"] = qualityConstraints.MaxVideoHeight;
                if (qualityConstraints.MaxVideoFps > 0)
                    videoEncoding["maxFramerate"] = qualityConstraints.MaxVideoFps;

                videoGrant["videoEncoding"] = videoEncoding;
            }

            // Screen share encoding constraint
            if (qualityConstraints.MaxScreenShareBitrateKbps > 0)
            {
                videoGrant["screenShareEncoding"] = new Dictionary<string, object>
                {
                    { "maxBitrate", qualityConstraints.MaxScreenShareBitrateKbps * 1000 }
                };
            }

            // Simulcast: when disabled, restrict published sources to a single quality layer.
            // When enabled (HD tier), allow the default multi-layer simulcast behaviour.
            if (!qualityConstraints.EnableSimulcast)
            {
                videoGrant["disableSimulcast"] = true;
            }
        }

        return BuildAccessToken(videoGrant, ttl, userId);
    }

    public async Task RemoveParticipantAsync(string roomName, string participantIdentity)
    {
        // Generate service-level token for LiveKit API
        var serviceToken = GenerateServiceToken();

        var requestBody = new
        {
            room = roomName,
            identity = participantIdentity
        };

        var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(ApiBaseUrl, "/twirp/livekit.RoomService/RemoveParticipant"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Add("Authorization", $"Bearer {serviceToken}");

        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendDataAsync(string roomName, string topic, string payload, CancellationToken ct)
    {
        var serviceToken = GenerateServiceToken();

        // LiveKit's SendData takes raw bytes; the twirp JSON mapping expects them
        // base64-encoded. RELIABLE ordering matters here: a stage update that
        // arrives after a later one would leave the composited layout wrong until
        // the next change.
        var requestBody = new
        {
            room = roomName,
            data = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)),
            kind = "RELIABLE",
            topic,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(ApiBaseUrl, "/twirp/livekit.RoomService/SendData"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Add("Authorization", $"Bearer {serviceToken}");

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private string GenerateServiceToken()
    {
        // Generate a service-level token with admin permissions
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddMinutes(5);

        return BuildAccessToken(
            new Dictionary<string, object> { { "roomAdmin", true } },
            expiry - now);
    }

    /// <summary>
    /// Generates a service token scoped to egress admin operations on the given room.
    /// LiveKit's egress twirp endpoints require <c>roomAdmin: true</c> for Start/Stop.
    /// When <paramref name="roomName"/> is null the token is room-unscoped (used for StopEgress
    /// when the original room is no longer known).
    /// </summary>
    private string GenerateEgressAdminToken(string? roomName)
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddMinutes(5);

        // Each of these gates a different call, and LiveKit answers a missing one
        // with a bare "permissions denied": `roomRecord` for starting and stopping
        // egress, `roomCreate` for making the broadcast room before it, and
        // `roomAdmin` for touching the room itself.
        var videoGrant = new Dictionary<string, object>
        {
            { "roomAdmin", true },
            { "roomRecord", true },
            { "roomCreate", true }
        };
        if (!string.IsNullOrEmpty(roomName))
        {
            videoGrant["room"] = roomName;
        }

        return BuildAccessToken(videoGrant, expiry - now);
    }

    public async Task<string> StartRoomCompositeEgressAsync(
        string roomName,
        string templateUrl,
        IEnumerable<EgressOutput> outputs,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            throw new ArgumentException("roomName is required", nameof(roomName));
        if (string.IsNullOrWhiteSpace(templateUrl))
            throw new ArgumentException("templateUrl is required", nameof(templateUrl));
        if (string.IsNullOrWhiteSpace(_options.EgressServiceUrl))
            throw new InvalidOperationException("LiveKitOptions.EgressServiceUrl is not configured.");

        var outputList = outputs?.ToList() ?? throw new ArgumentNullException(nameof(outputs));
        if (outputList.Count == 0)
            throw new ArgumentException("At least one egress output is required.", nameof(outputs));

        // Segregate outputs by type. LiveKit's RoomCompositeEgressRequest expects
        // segment_outputs (HLS) and stream_outputs (RTMP) as parallel arrays.
        var segmentOutputs = outputList.OfType<HlsEgressOutput>()
            .Select(BuildSegmentOutput)
            .ToList();

        var rtmpOutputs = outputList.OfType<RtmpEgressOutput>().ToList();
        var streamOutputs = rtmpOutputs.Count == 0
            ? null
            : new[]
            {
                new Dictionary<string, object?>
                {
                    ["protocol"] = "rtmp",
                    ["urls"] = rtmpOutputs.Select(r => r.Url).ToArray()
                }
            };

        // Build the request body. Only include segment/stream arrays when populated,
        // and only include the advanced encoder block when at least one RTMP output
        // specified an override (or we need the tier's default "sane" settings).
        var body = new Dictionary<string, object?>
        {
            ["room_name"] = roomName,
            ["layout"] = "custom",
            ["custom_base_url"] = templateUrl,
            ["audio_only"] = false,
            ["video_only"] = false
        };

        if (segmentOutputs.Count > 0)
            body["segment_outputs"] = segmentOutputs;
        if (streamOutputs != null)
            body["stream_outputs"] = streamOutputs;

        // Encoder settings: LiveKit RoomCompositeEgress uses a single video encoding
        // for all outputs. Take the first RTMP override if one exists; otherwise fall
        // back to sane MVP defaults.
        var encoderSource = rtmpOutputs.FirstOrDefault(r =>
            r.VideoBitrateKbps.HasValue || r.Width.HasValue || r.Height.HasValue);

        if (encoderSource != null || rtmpOutputs.Count > 0)
        {
            var bitrate = encoderSource?.VideoBitrateKbps ?? 2500;
            var width = encoderSource?.Width ?? 1280;
            var height = encoderSource?.Height ?? 720;

            body["advanced"] = new Dictionary<string, object?>
            {
                ["video_codec"] = "H264_MAIN",
                ["video_bitrate"] = bitrate,
                ["width"] = width,
                ["height"] = height,
                ["framerate"] = 30
            };
        }

        var json = JsonSerializer.Serialize(body, EgressJsonOptions);
        // The room has to exist before anything can be recorded out of it.
        // LiveKit's auto_create makes a room when a *participant* joins, and the
        // host's media connection is still being established when this runs - so
        // without this the very first broadcast of a channel was refused with
        // "requested room does not exist", and whether it worked depended on who
        // happened to already be in the room.
        await EnsureRoomExistsAsync(roomName, ct).ConfigureAwait(false);

        var url = CombineUrl(ApiBaseUrl, "/twirp/livekit.Egress/StartRoomCompositeEgress");

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateEgressAdminToken(roomName));

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"LiveKit StartRoomCompositeEgress failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("egress_id", out var egressIdProp)
            || egressIdProp.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"LiveKit StartRoomCompositeEgress response missing egress_id. Body: {responseBody}");
        }

        return egressIdProp.GetString()!;
    }

    public async Task StopEgressAsync(string egressId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(egressId))
            throw new ArgumentException("egressId is required", nameof(egressId));
        if (string.IsNullOrWhiteSpace(_options.EgressServiceUrl))
            throw new InvalidOperationException("LiveKitOptions.EgressServiceUrl is not configured.");

        var body = new { egress_id = egressId };
        var json = JsonSerializer.Serialize(body);
        var url = CombineUrl(ApiBaseUrl, "/twirp/livekit.Egress/StopEgress");

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateEgressAdminToken(null));

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException(
                $"LiveKit StopEgress failed for {egressId}: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}");
        }
    }

    public async Task<string> RestartEgressWithNewOutputsAsync(
        string oldEgressId,
        string roomName,
        string templateUrl,
        IEnumerable<EgressOutput> outputs,
        CancellationToken ct)
    {
        // Tolerate failures from StopEgress - the prior egress may have ended naturally
        // (room empty, compositor crash, already-stopped) and we still want to start the
        // replacement. Log so operators can investigate if it becomes a pattern.
        try
        {
            await StopEgressAsync(oldEgressId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "StopEgress failed for {EgressId} during restart; proceeding with new egress start",
                oldEgressId);
        }

        return await StartRoomCompositeEgressAsync(roomName, templateUrl, outputs, ct)
            .ConfigureAwait(false);
    }

    private static Dictionary<string, object?> BuildSegmentOutput(HlsEgressOutput hls)
    {
        return new Dictionary<string, object?>
        {
            ["protocol"] = "hls",
            ["filename_prefix"] = hls.SegmentPrefix,
            ["playlist_name"] = hls.PlaylistName,
            ["segment_duration"] = hls.SegmentDurationSeconds,
            ["s3"] = new Dictionary<string, object?>
            {
                ["access_key"] = hls.AccessKey,
                ["secret"] = hls.AccessSecret,
                ["region"] = hls.Region,
                ["endpoint"] = hls.Endpoint,
                ["bucket"] = hls.Bucket,
                ["force_path_style"] = hls.ForcePathStyle
            }
        };
    }

    /// <summary>
    /// Create the room if it is not there yet. Idempotent: LiveKit answers an
    /// existing room with its current state rather than an error.
    /// </summary>
    private async Task EnsureRoomExistsAsync(string roomName, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            CombineUrl(ApiBaseUrl, "/twirp/livekit.RoomService/CreateRoom"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { name = roomName }),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateEgressAdminToken(roomName));

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException(
                $"LiveKit CreateRoom failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }
    }

    /// <summary>
    /// Base URL for LiveKit's own HTTP APIs (RoomService, Egress).
    /// </summary>
    /// <remarks>
    /// Not <see cref="LiveKitOptions.Host"/>: that is the address handed to
    /// browsers, so it is a public `wss://` URL that no HttpClient can dial.
    /// Server-to-server calls use the API base, falling back to Host rewritten
    /// to http(s) when none is configured - which is what a single-host
    /// deployment, where the two are the same machine, actually wants.
    /// </remarks>
    private string ApiBaseUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_options.EgressServiceUrl))
                return _options.EgressServiceUrl!;

            var host = _options.Host ?? string.Empty;
            if (host.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                return "https://" + host["wss://".Length..];
            if (host.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
                return "http://" + host["ws://".Length..];
            return host;
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var trimmedPath = path.StartsWith('/') ? path : "/" + path;
        return trimmedBase + trimmedPath;
    }
}
