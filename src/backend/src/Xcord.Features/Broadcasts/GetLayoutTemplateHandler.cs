using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Xcord.Infrastructure.Options;

namespace Xcord.Features.Broadcasts;

/// <summary>
/// Serves the HTML layout template rendered by LiveKit Egress's headless Chrome.
/// The template connects to the LiveKit room as a read-only participant, subscribes
/// to tracks for the specified slots, and arranges them per the preset's layout.
/// </summary>
public sealed class GetLayoutTemplateHandler : IEndpoint
{
    private static readonly string[] SupportedPresets =
        new[] { "grid", "spotlight", "pip", "side-by-side" };

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/broadcast-layout/{preset}", (
            string preset,
            string? room,
            string? token,
            string? slots,
            [FromServices] IOptions<LiveKitOptions> livekitOptions) =>
        {
            var normalized = (preset ?? "").ToLowerInvariant();
            if (!SupportedPresets.Contains(normalized))
                return Results.NotFound();

            // Validate slots JSON is parseable. If invalid, treat as empty to avoid
            // reflecting un-parseable content into the script context.
            var slotsJson = slots ?? "[]";
            if (!TryValidateSlots(slotsJson))
                slotsJson = "[]";

            var livekitUrl = ResolveLiveKitWsUrl(livekitOptions.Value);

            var html = RenderLayout(normalized, room ?? "", token ?? "", slotsJson, livekitUrl);
            return Results.Content(html, "text/html; charset=utf-8");
        })
        .AllowAnonymous()
        .WithName("GetBroadcastLayoutTemplate")
        .WithTags("Broadcasts");
    }

    private static bool TryValidateSlots(string slotsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(slotsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) return false;
                if (!el.TryGetProperty("userId", out _)) return false;
                if (!el.TryGetProperty("slotIndex", out var idx)) return false;
                if (idx.ValueKind != JsonValueKind.Number) return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveLiveKitWsUrl(LiveKitOptions options)
    {
        // The LiveKit client SDK expects a ws:// or wss:// URL.
        // LiveKitOptions.Host holds the server host; if it's already a URL, use as-is.
        var host = options.Host ?? "";
        if (host.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return host;
        }
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return "ws://" + host.Substring(7);
        if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return "wss://" + host.Substring(8);
        if (string.IsNullOrEmpty(host))
            return "ws://livekit:7880";
        return "wss://" + host;
    }

    private static string RenderLayout(
        string preset,
        string room,
        string token,
        string slots,
        string livekitUrl)
    {
        var css = preset switch
        {
            "grid" => GridCss,
            "spotlight" => SpotlightCss,
            "pip" => PipCss,
            "side-by-side" => SideBySideCss,
            _ => GridCss,
        };
        var slotElements = GenerateSlotElements(preset);

        var jsEnc = JavaScriptEncoder.Default;
        var escapedRoomAttr = HtmlEncoder.Default.Encode(room);
        var escapedRoomJs = jsEnc.Encode(room);
        var escapedTokenJs = jsEnc.Encode(token);
        var escapedSlotsJs = jsEnc.Encode(slots);
        var escapedLiveKitJs = jsEnc.Encode(livekitUrl);
        var escapedPresetAttr = HtmlEncoder.Default.Encode(preset);

        return TemplateCommon
            .Replace("{{CSS}}", css)
            .Replace("{{SLOTS_HTML}}", slotElements)
            .Replace("{{ROOM_ATTR}}", escapedRoomAttr)
            .Replace("{{PRESET_ATTR}}", escapedPresetAttr)
            .Replace("{{ROOM}}", escapedRoomJs)
            .Replace("{{TOKEN}}", escapedTokenJs)
            .Replace("{{SLOTS}}", escapedSlotsJs)
            .Replace("{{LIVEKIT_URL}}", escapedLiveKitJs);
    }

    private static string GenerateSlotElements(string preset)
    {
        return preset switch
        {
            "grid" => BuildSlots("grid", 8),
            "spotlight" => BuildSpotlightSlots(4),
            "pip" => BuildPipSlots(),
            "side-by-side" => BuildSlots("side-by-side", 2),
            _ => BuildSlots("grid", 8),
        };
    }

    private static string BuildSlots(string containerClass, int count)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"<div class=\"stage {containerClass}\">");
        for (int i = 0; i < count; i++)
        {
            sb.Append($"<div class=\"slot\" data-slot-index=\"{i}\"></div>");
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string BuildSpotlightSlots(int thumbnailCount)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<div class=\"stage spotlight\">");
        sb.Append("<div class=\"spotlight-main slot\" data-slot-index=\"0\"></div>");
        sb.Append("<div class=\"spotlight-thumbs\">");
        for (int i = 1; i <= thumbnailCount; i++)
        {
            sb.Append($"<div class=\"slot\" data-slot-index=\"{i}\"></div>");
        }
        sb.Append("</div>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string BuildPipSlots()
    {
        return "<div class=\"stage pip\">" +
               "<div class=\"pip-main slot\" data-slot-index=\"0\"></div>" +
               "<div class=\"pip-overlay slot\" data-slot-index=\"1\"></div>" +
               "</div>";
    }

    // ---------- CSS per preset ----------

    private const string BaseCss = @"
        * { margin: 0; padding: 0; box-sizing: border-box; }
        html, body { width: 100%; height: 100%; background: #000; overflow: hidden; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; color: #fff; }
        .stage { width: 100vw; height: 100vh; background: #000; }
        .slot { background: #111; overflow: hidden; position: relative; }
        .slot video, .slot audio { width: 100%; height: 100%; object-fit: cover; display: block; }
    ";

    private const string GridCss = BaseCss + @"
        .stage.grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(25%, 1fr));
            grid-auto-rows: 1fr;
            gap: 2px;
            background: #000;
        }
    ";

    private const string SpotlightCss = BaseCss + @"
        .stage.spotlight {
            display: grid;
            grid-template-columns: 70% 30%;
            gap: 2px;
        }
        .stage.spotlight .spotlight-main { height: 100vh; }
        .stage.spotlight .spotlight-thumbs {
            display: grid;
            grid-template-rows: repeat(4, 1fr);
            gap: 2px;
            height: 100vh;
        }
    ";

    private const string PipCss = BaseCss + @"
        .stage.pip { position: relative; }
        .stage.pip .pip-main { position: absolute; inset: 0; width: 100%; height: 100%; }
        .stage.pip .pip-overlay {
            position: absolute;
            right: 2%;
            bottom: 2%;
            width: 25%;
            height: 25%;
            border: 2px solid #222;
            border-radius: 6px;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.6);
            background: #111;
        }
    ";

    private const string SideBySideCss = BaseCss + @"
        .stage.side-by-side {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 2px;
        }
        .stage.side-by-side .slot { height: 100vh; }
    ";

    // ---------- Common HTML/JS template ----------

    private const string TemplateCommon = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title>Broadcast Layout - {{PRESET_ATTR}}</title>
    <style>{{CSS}}</style>
    <script src=""https://cdn.jsdelivr.net/npm/livekit-client@2.7.2/dist/livekit-client.umd.min.js""></script>
</head>
<body data-preset=""{{PRESET_ATTR}}"" data-room=""{{ROOM_ATTR}}"">
    {{SLOTS_HTML}}
    <div id=""audio-sink"" style=""display:none""></div>
    <script>
        (function () {
            window.__LIVEKIT_URL__ = window.__LIVEKIT_URL__ || ""{{LIVEKIT_URL}}"";
            var params = new URLSearchParams(window.location.search);
            var roomName = params.get('room') || ""{{ROOM}}"";
            var token = params.get('token') || ""{{TOKEN}}"";
            var slotsRaw = params.get('slots');
            var slots = [];
            try { slots = JSON.parse(slotsRaw || ""{{SLOTS}}"" || '[]'); } catch (e) { slots = []; }
            if (!Array.isArray(slots)) slots = [];

            function getSlotElement(userId) {
                var match = null;
                for (var i = 0; i < slots.length; i++) {
                    if (String(slots[i].userId) === String(userId)) { match = slots[i]; break; }
                }
                if (!match) return null;
                return document.querySelector('[data-slot-index=""' + String(match.slotIndex) + '""]');
            }

            function attachTrack(track, participant) {
                if (!track) return;
                if (track.kind === 'video') {
                    var container = getSlotElement(participant.identity);
                    if (!container) return;
                    var el = track.attach();
                    el.style.width = '100%';
                    el.style.height = '100%';
                    el.style.objectFit = 'cover';
                    container.innerHTML = '';
                    container.appendChild(el);
                } else if (track.kind === 'audio') {
                    var sink = document.getElementById('audio-sink');
                    var audioEl = track.attach();
                    audioEl.style.display = 'none';
                    sink.appendChild(audioEl);
                }
            }

            function detachTrack(track) {
                if (!track) return;
                try {
                    var elements = track.detach();
                    if (Array.isArray(elements)) {
                        elements.forEach(function (el) { if (el && el.remove) el.remove(); });
                    }
                } catch (e) { /* swallow */ }
            }

            if (!window.LivekitClient) {
                console.error('LiveKit client SDK failed to load');
                return;
            }

            var Room = window.LivekitClient.Room;
            var RoomEvent = window.LivekitClient.RoomEvent;

            var room = new Room({
                adaptiveStream: false,
                dynacast: false,
            });

            room.on(RoomEvent.TrackSubscribed, function (track, publication, participant) {
                attachTrack(track, participant);
            });

            room.on(RoomEvent.TrackUnsubscribed, function (track) {
                detachTrack(track);
            });

            room.on(RoomEvent.Disconnected, function () {
                console.log('LiveKit room disconnected');
            });

            room.connect(window.__LIVEKIT_URL__, token).then(function () {
                // Attach any tracks already subscribed (edge case: fast-start).
                room.remoteParticipants.forEach(function (p) {
                    p.trackPublications.forEach(function (pub) {
                        if (pub.track && pub.isSubscribed) {
                            attachTrack(pub.track, p);
                        }
                    });
                });
                window.__layoutReady = true;
            }).catch(function (err) {
                console.error('LiveKit connect failed', err);
            });
        })();
    </script>
</body>
</html>";
}
