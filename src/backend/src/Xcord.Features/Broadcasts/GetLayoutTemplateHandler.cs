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
        new[] { "grid", "spotlight", "pip", "side-by-side", "audio-show" };

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
        var jsEnc = JavaScriptEncoder.Default;

        return TemplateCommon
            .Replace("{{CSS}}", AllCss)
            .Replace("{{ROOM_ATTR}}", HtmlEncoder.Default.Encode(room))
            .Replace("{{PRESET_ATTR}}", HtmlEncoder.Default.Encode(preset))
            .Replace("{{ROOM}}", jsEnc.Encode(room))
            .Replace("{{TOKEN}}", jsEnc.Encode(token))
            .Replace("{{SLOTS}}", jsEnc.Encode(slots))
            .Replace("{{PRESET}}", jsEnc.Encode(preset))
            .Replace("{{CONTROL_TOPIC}}", jsEnc.Encode(BroadcastControl.Topic))
            .Replace("{{LIVEKIT_URL}}", jsEnc.Encode(livekitUrl));
    }

    // ---------- CSS per preset ----------

    private const string BaseCss = @"
        * { margin: 0; padding: 0; box-sizing: border-box; }
        html, body { width: 100%; height: 100%; background: #000; overflow: hidden; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; color: #fff; }
        .stage { width: 100vw; height: 100vh; background: #000; }
        .slot { background: #111; overflow: hidden; position: relative; }
        .slot video, .slot audio { width: 100%; height: 100%; object-fit: cover; display: block; }
    ";

    private const string GridCss = @"
        .stage.grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(25%, 1fr));
            grid-auto-rows: 1fr;
            gap: 2px;
            background: #000;
        }
    ";

    private const string SpotlightCss = @"
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

    private const string PipCss = @"
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

    private const string SideBySideCss = @"
        .stage.side-by-side {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 2px;
        }
        .stage.side-by-side .slot { height: 100vh; }
    ";

    private const string AudioShowCss = @"
        .stage.audio-show {
            display: flex;
            flex-wrap: wrap;
            align-content: center;
            justify-content: center;
            gap: 24px;
            padding: 48px;
            background: #131114;
        }
        .stage.audio-show .slot {
            background: #1a171b;
            border: 1px solid rgba(255, 240, 225, 0.09);
            border-radius: 14px;
            min-width: 280px;
            height: 132px;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            gap: 14px;
            padding: 20px 28px;
        }
        .stage.audio-show .slot:empty { display: none; }
        .stage.audio-show .speaker-name {
            font-size: 20px;
            font-weight: 600;
            letter-spacing: -0.01em;
            color: #f0ede8;
        }
        .stage.audio-show .wave {
            display: flex;
            align-items: flex-end;
            gap: 4px;
            height: 34px;
        }
        .stage.audio-show .wave i {
            display: block;
            width: 4px;
            height: 100%;
            border-radius: 2px;
            background: #6a6361;
            transform: scaleY(0.18);
            transform-origin: bottom;
            transition: transform 90ms linear, background-color 160ms linear;
        }
        /* Amber is the only colour that means ""this one is talking"". */
        .stage.audio-show .slot.speaking .wave i { background: #d4943a; }
    ";

    /// <summary>
    /// Every preset's rules, always. The renderer switches presets by changing a
    /// class on one element, so the stylesheet cannot be preset-specific.
    /// </summary>
    private const string AllCss = BaseCss + GridCss + SpotlightCss + PipCss + SideBySideCss + AudioShowCss;

    // ---------- Common HTML/JS template ----------

    private const string TemplateCommon = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title>Broadcast Layout</title>
    <style>{{CSS}}</style>
    <script src=""https://cdn.jsdelivr.net/npm/livekit-client@2.7.2/dist/livekit-client.umd.min.js""></script>
</head>
<body data-preset=""{{PRESET_ATTR}}"" data-room=""{{ROOM_ATTR}}"">
    <div id=""stage"" class=""stage""></div>
    <div id=""audio-sink"" style=""display:none""></div>
    <script>
        (function () {
            window.__LIVEKIT_URL__ = window.__LIVEKIT_URL__ || ""{{LIVEKIT_URL}}"";
            var params = new URLSearchParams(window.location.search);
            var roomName = params.get('room') || ""{{ROOM}}"";
            var token = params.get('token') || ""{{TOKEN}}"";
            var CONTROL_TOPIC = ""{{CONTROL_TOPIC}}"";

            // Shape of each preset: the class on the stage container, how many
            // slots it has, and any extra classes particular slots carry.
            var PRESETS = {
                'grid':         { slots: 8, classes: {} },
                'spotlight':    { slots: 5, classes: { 0: 'spotlight-main' }, thumbsFrom: 1 },
                'pip':          { slots: 2, classes: { 0: 'pip-main', 1: 'pip-overlay' } },
                'side-by-side': { slots: 2, classes: {} },
                'audio-show':   { slots: 0, classes: {}, audioOnly: true }
            };

            function normalizePreset(name) {
                return Object.prototype.hasOwnProperty.call(PRESETS, name) ? name : 'grid';
            }

            function normalizeSlots(value) {
                if (!Array.isArray(value)) return [];
                var out = [];
                for (var i = 0; i < value.length; i++) {
                    var entry = value[i];
                    if (!entry || typeof entry !== 'object') continue;
                    if (entry.userId === undefined || entry.userId === null) continue;
                    var index = Number(entry.slotIndex);
                    if (!isFinite(index) || index < 0) continue;
                    out.push({ userId: String(entry.userId), slotIndex: index });
                }
                return out;
            }

            var preset = normalizePreset(""{{PRESET}}"");
            var slots = [];
            try { slots = normalizeSlots(JSON.parse(params.get('slots') || ""{{SLOTS}}"" || '[]')); }
            catch (e) { slots = []; }

            // Every video track we are subscribed to, by participant identity. Kept
            // so a stage or preset change can re-place them without renegotiating.
            var videoTracks = {};
            var stageEl = document.getElementById('stage');

            // Display name per identity, learned from the room. Falls back to the
            // identity so a card is never blank.
            var names = {};

            function buildAudioShowStage() {
                stageEl.innerHTML = '';
                for (var i = 0; i < slots.length; i++) {
                    var id = slots[i].userId;
                    var card = document.createElement('div');
                    card.className = 'slot';
                    card.setAttribute('data-identity', id);
                    card.setAttribute('data-slot-index', String(slots[i].slotIndex));

                    var name = document.createElement('div');
                    name.className = 'speaker-name';
                    name.textContent = names[id] || id;
                    card.appendChild(name);

                    var wave = document.createElement('div');
                    wave.className = 'wave';
                    for (var b = 0; b < 5; b++) wave.appendChild(document.createElement('i'));
                    card.appendChild(wave);

                    stageEl.appendChild(card);
                }
            }

            function buildStage() {
                var shape = PRESETS[preset];
                stageEl.className = 'stage ' + preset;

                if (shape.audioOnly) {
                    buildAudioShowStage();
                    return;
                }

                stageEl.innerHTML = '';

                var thumbs = null;
                if (preset === 'spotlight') {
                    thumbs = document.createElement('div');
                    thumbs.className = 'spotlight-thumbs';
                }

                for (var i = 0; i < shape.slots; i++) {
                    var slot = document.createElement('div');
                    slot.className = 'slot' + (shape.classes[i] ? ' ' + shape.classes[i] : '');
                    slot.setAttribute('data-slot-index', String(i));
                    if (thumbs && shape.thumbsFrom !== undefined && i >= shape.thumbsFrom) {
                        thumbs.appendChild(slot);
                    } else {
                        stageEl.appendChild(slot);
                    }
                }
                if (thumbs) stageEl.appendChild(thumbs);
            }

            function slotElementFor(identity) {
                for (var i = 0; i < slots.length; i++) {
                    if (slots[i].userId === String(identity)) {
                        return stageEl.querySelector('[data-slot-index=""' + slots[i].slotIndex + '""]');
                    }
                }
                return null;
            }

            // Rebuild the stage and place every known track into it. Called on any
            // change to the preset or the assignments; cheap enough to redo whole
            // rather than diff, and a full redraw cannot drift out of sync.
            function render() {
                buildStage();
                if (PRESETS[preset].audioOnly) return;
                Object.keys(videoTracks).forEach(function (identity) {
                    var container = slotElementFor(identity);
                    if (!container) return;
                    try {
                        var el = videoTracks[identity].attach();
                        el.style.width = '100%';
                        el.style.height = '100%';
                        el.style.objectFit = 'cover';
                        container.innerHTML = '';
                        container.appendChild(el);
                    } catch (e) { /* track ended mid-render */ }
                });
            }

            function applyControl(text) {
                var next;
                try { next = JSON.parse(text); } catch (e) { return; }
                if (!next || typeof next !== 'object') return;
                if (typeof next.preset === 'string') preset = normalizePreset(next.preset);
                if (next.slots !== undefined) slots = normalizeSlots(next.slots);
                document.body.setAttribute('data-preset', preset);
                render();
            }

            if (!window.LivekitClient) {
                console.error('LiveKit client SDK failed to load');
                return;
            }

            var Room = window.LivekitClient.Room;
            var RoomEvent = window.LivekitClient.RoomEvent;
            var room = new Room({ adaptiveStream: false, dynacast: false });

            room.on(RoomEvent.TrackSubscribed, function (track, publication, participant) {
                if (!track) return;
                if (track.kind === 'video') {
                    videoTracks[String(participant.identity)] = track;
                    render();
                } else if (track.kind === 'audio') {
                    // Audio is mixed regardless of stage position: someone speaking
                    // is heard whether or not the layout has a tile for them.
                    var audioEl = track.attach();
                    audioEl.style.display = 'none';
                    document.getElementById('audio-sink').appendChild(audioEl);
                }
            });

            room.on(RoomEvent.TrackUnsubscribed, function (track, publication, participant) {
                if (!track) return;
                if (track.kind === 'video' && participant) {
                    delete videoTracks[String(participant.identity)];
                }
                try {
                    var elements = track.detach();
                    if (Array.isArray(elements)) {
                        elements.forEach(function (el) { if (el && el.remove) el.remove(); });
                    }
                } catch (e) { /* swallow */ }
                if (track.kind === 'video') render();
            });

            // Stage and layout changes arrive here rather than as a new page load,
            // which is what keeps the outgoing stream unbroken across them.
            room.on(RoomEvent.DataReceived, function (payload, participant, kind, topic) {
                if (topic !== CONTROL_TOPIC) return;
                try {
                    applyControl(new TextDecoder().decode(payload));
                } catch (e) { /* malformed control message */ }
            });

            // The waveform is the whole visual in audio-show, so it has to track
            // the real signal rather than animate on a timer. LiveKit reports both
            // who is speaking and how loudly.
            function paintLevels(speakers) {
                if (!PRESETS[preset].audioOnly) return;
                var loud = {};
                (speakers || []).forEach(function (p) {
                    loud[String(p.identity)] = typeof p.audioLevel === 'number' ? p.audioLevel : 0;
                });

                var cards = stageEl.querySelectorAll('.slot');
                for (var i = 0; i < cards.length; i++) {
                    var card = cards[i];
                    var id = card.getAttribute('data-identity');
                    var level = loud[id];
                    var speaking = level !== undefined;
                    card.classList.toggle('speaking', speaking);

                    var bars = card.querySelectorAll('.wave i');
                    for (var b = 0; b < bars.length; b++) {
                        // Idle bars sit low and flat; a speaking card's bars vary
                        // per bar so it reads as a waveform, not a level meter.
                        var scale = speaking
                            ? Math.min(1, 0.25 + (level || 0) * (0.7 + 0.3 * Math.sin((b + 1) * 1.7)))
                            : 0.18;
                        bars[b].style.transform = 'scaleY(' + scale.toFixed(3) + ')';
                    }
                }
            }

            function rememberName(participant) {
                if (!participant) return false;
                var id = String(participant.identity);
                var name = participant.name || participant.identity;
                if (names[id] === name) return false;
                names[id] = name;
                return true;
            }

            room.on(RoomEvent.ActiveSpeakersChanged, paintLevels);

            room.on(RoomEvent.ParticipantConnected, function (participant) {
                if (rememberName(participant)) render();
            });

            room.on(RoomEvent.Disconnected, function () {
                console.log('LiveKit room disconnected');
            });

            render();

            room.connect(window.__LIVEKIT_URL__, token).then(function () {
                room.remoteParticipants.forEach(function (p) {
                    rememberName(p);
                    p.trackPublications.forEach(function (pub) {
                        if (pub.track && pub.isSubscribed) {
                            if (pub.track.kind === 'video') {
                                videoTracks[String(p.identity)] = pub.track;
                            }
                        }
                    });
                });
                render();
                window.__layoutReady = true;
            }).catch(function (err) {
                console.error('LiveKit connect failed', err);
            });
        })();
    </script>
</body>
</html>";
}
