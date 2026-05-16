using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Broadcasts;

/// <summary>
/// Receives LiveKit egress lifecycle webhooks and reconciles the local broadcast state.
/// LiveKit delivers webhooks with a signed JWT in the Authorization header whose body
/// is a hash of the request payload; we verify both the signature and the hash.
/// </summary>
public sealed class EgressWebhookHandler : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/internal/egress-webhook", async (
            HttpRequest httpRequest,
            [FromServices] AppDbContext dbContext,
            [FromServices] INotificationService notificationService,
            [FromServices] IOptions<LiveKitOptions> livekitOptions,
            [FromServices] IConnectionMultiplexer redis,
            [FromServices] IOptions<RedisOptions> redisOptions,
            [FromServices] ILogger<EgressWebhookHandler> logger,
            CancellationToken ct) =>
        {
            var opts = livekitOptions.Value;

            // Buffer the body once so we can both hash it for signature verification and
            // parse it into the event payload.
            httpRequest.EnableBuffering();
            string body;
            using (var reader = new StreamReader(
                httpRequest.Body, Encoding.UTF8, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                httpRequest.Body.Position = 0;
            }

            var authHeader = httpRequest.Headers.Authorization.ToString();
            if (!TryExtractBearer(authHeader, out var token))
            {
                logger.LogWarning("Egress webhook rejected: missing bearer token");
                return Results.Unauthorized();
            }

            if (!VerifyLiveKitWebhookJwt(token, body, opts, logger))
            {
                logger.LogWarning("Egress webhook rejected: invalid JWT signature or body hash");
                return Results.Unauthorized();
            }

            EgressEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<EgressEvent>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Egress webhook body failed to parse");
                return Results.BadRequest();
            }
            if (evt == null || string.IsNullOrEmpty(evt.Event) || evt.EgressInfo == null)
            {
                logger.LogWarning("Egress webhook missing required fields");
                return Results.BadRequest();
            }

            var egressId = evt.EgressInfo.EgressId ?? string.Empty;
            if (string.IsNullOrEmpty(egressId))
            {
                logger.LogWarning("Egress webhook missing egress_id");
                return Results.BadRequest();
            }

            // Replay protection: LiveKit retries webhooks on non-2xx responses, and a
            // captured-and-replayed JWT remains valid for its full lifetime. NX-set a
            // per-(egress,event) marker so each event is applied at most once.
            var redisDb = redis.GetDatabase();
            var idempotencyKey =
                $"{redisOptions.Value.ChannelPrefix}:lk_egress:{egressId}:{evt.Event}";
            var firstDelivery = await redisDb.StringSetAsync(
                idempotencyKey, "1", TimeSpan.FromMinutes(10), When.NotExists);
            if (!firstDelivery)
            {
                logger.LogDebug(
                    "Egress webhook replay suppressed for egress {EgressId} event {Event}",
                    egressId, evt.Event);
                return Results.Ok();
            }

            var broadcast = await dbContext.Broadcasts
                .Include(b => b.Channel)
                .FirstOrDefaultAsync(b => b.EgressJobId == egressId, ct);
            if (broadcast == null)
            {
                // Unknown egress - this can legitimately happen after a broadcast is ended
                // locally but LiveKit is still draining events. Ack so LiveKit doesn't retry.
                logger.LogInformation(
                    "Egress webhook for unknown egress {EgressId} ({Event}); acknowledging",
                    egressId, evt.Event);
                return Results.Ok();
            }

            var now = DateTimeOffset.UtcNow;
            var broadcastChanged = false;

            switch (evt.Event)
            {
                case "egress_started":
                    // A Live, Ended, or Failed broadcast must never regress to Starting/Live
                    // from a stale or replayed start event. Only honor this transition out of
                    // the Starting state.
                    if (broadcast.Status == BroadcastStatus.Starting)
                    {
                        broadcast.Status = BroadcastStatus.Live;
                        broadcastChanged = true;
                    }
                    else
                    {
                        logger.LogWarning(
                            "Ignoring egress_started for broadcast {BroadcastId} in status {Status}",
                            broadcast.Id, broadcast.Status);
                    }
                    break;

                case "egress_ended":
                    if (broadcast.Status == BroadcastStatus.Ended
                        || broadcast.Status == BroadcastStatus.Failed)
                    {
                        logger.LogDebug(
                            "Ignoring egress_ended for broadcast {BroadcastId} already in terminal status {Status}",
                            broadcast.Id, broadcast.Status);
                        break;
                    }
                    broadcast.Status = BroadcastStatus.Ended;
                    broadcast.EndedAt = now;
                    broadcastChanged = true;
                    await MarkActiveStreambotsAsync(
                        dbContext, broadcast.Id,
                        BroadcastStreambotStatus.Ended,
                        lastError: null, now, ct);
                    break;

                case "egress_failed":
                    // Don't overwrite a cleanly Ended broadcast with a late Failed event.
                    // Both Ended and Failed are terminal; first writer wins.
                    if (broadcast.Status == BroadcastStatus.Ended
                        || broadcast.Status == BroadcastStatus.Failed)
                    {
                        logger.LogWarning(
                            "Ignoring egress_failed for broadcast {BroadcastId} already in terminal status {Status}",
                            broadcast.Id, broadcast.Status);
                        break;
                    }
                    broadcast.Status = BroadcastStatus.Failed;
                    broadcast.EndedAt = now;
                    broadcastChanged = true;
                    await MarkActiveStreambotsAsync(
                        dbContext, broadcast.Id,
                        BroadcastStreambotStatus.Failed,
                        lastError: evt.EgressInfo.Error ?? "Egress failed",
                        now, ct);
                    break;

                case "egress_updated":
                    // Per-relay status updates keyed by RTMP URL.
                    if (evt.EgressInfo.StreamResults is { Count: > 0 } results)
                    {
                        await ApplyStreamResultsAsync(
                            dbContext, notificationService, broadcast, results, now, ct);
                    }
                    break;

                default:
                    logger.LogDebug("Ignoring unrecognized egress event {Event}", evt.Event);
                    break;
            }

            if (broadcastChanged)
            {
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                await notificationService.NotifyConversationAsync(
                    broadcast.Channel.ConversationId,
                    "Broadcast_StatusChanged",
                    new
                    {
                        broadcastId = broadcast.Id,
                        channelId = broadcast.ChannelId,
                        status = broadcast.Status.ToString(),
                        endedAt = broadcast.EndedAt
                    }, ct);
            }

            return Results.Ok();
        })
        .AllowAnonymous()
        .WithName("EgressWebhook")
        .WithTags("Broadcasts");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private static bool TryExtractBearer(string header, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrEmpty(header))
            return false;
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        token = header.Substring(prefix.Length).Trim();
        return token.Length > 0;
    }

    /// <summary>
    /// Validates the LiveKit webhook JWT: signature with the API secret, and the
    /// embedded <c>sha256</c> claim must match the SHA-256 hash of the request body.
    /// </summary>
    private static bool VerifyLiveKitWebhookJwt(
        string token,
        string body,
        LiveKitOptions opts,
        ILogger logger)
    {
        // Prefer the explicit webhook secret if configured (allows rotating webhook
        // credentials independently of the LiveKit API key). Fall back to ApiSecret,
        // which is what LiveKit uses by default to sign webhook JWTs.
        var signingSecret = !string.IsNullOrEmpty(opts.EgressWebhookSecret)
            ? opts.EgressWebhookSecret!
            : opts.ApiSecret;
        if (string.IsNullOrEmpty(signingSecret))
        {
            logger.LogError("Cannot verify egress webhook: no signing secret configured");
            return false;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret));
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            handler.ValidateToken(token, validationParams, out var validated);
            var jwt = (JwtSecurityToken)validated;

            // LiveKit embeds a base64-encoded SHA-256 of the body in the "sha256" claim.
            var expectedHash = jwt.Claims.FirstOrDefault(c => c.Type == "sha256")?.Value;
            if (string.IsNullOrEmpty(expectedHash))
            {
                logger.LogWarning("Egress webhook JWT missing sha256 claim");
                return false;
            }

            var actualHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(body)));
            if (!CryptographicEquals(expectedHash, actualHash))
            {
                logger.LogWarning("Egress webhook body hash mismatch");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Egress webhook JWT validation failed");
            return false;
        }
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private static async Task MarkActiveStreambotsAsync(
        AppDbContext db,
        long broadcastId,
        BroadcastStreambotStatus newStatus,
        string? lastError,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var bots = await db.BroadcastStreambots
            .Where(bs => bs.BroadcastId == broadcastId
                && bs.Status != BroadcastStreambotStatus.Ended
                && bs.Status != BroadcastStreambotStatus.Failed)
            .ToListAsync(ct);
        foreach (var bs in bots)
        {
            bs.Status = newStatus;
            bs.EndedAt = now;
            if (!string.IsNullOrEmpty(lastError))
                bs.LastError = lastError;
        }
    }

    private static async Task ApplyStreamResultsAsync(
        AppDbContext db,
        INotificationService notificationService,
        Broadcast broadcast,
        List<EgressStreamResult> results,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var bots = await db.BroadcastStreambots
            .Where(bs => bs.BroadcastId == broadcast.Id)
            .Include(bs => bs.StreamBot)
            .ToListAsync(ct);
        if (bots.Count == 0)
            return;

        var changed = new List<BroadcastStreambot>();
        foreach (var result in results)
        {
            if (string.IsNullOrEmpty(result.Url))
                continue;

            // Match by RTMP URL prefix - the per-relay URL in the stream result includes
            // the full URL with key, which matches StreamBot.RtmpUrl as a prefix. We
            // compare prefixes rather than full URLs because LiveKit may URL-encode
            // differently than we did when submitting.
            var bot = bots.FirstOrDefault(bs =>
                bs.StreamBot?.RtmpUrl != null
                && result.Url!.StartsWith(bs.StreamBot.RtmpUrl.TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase));
            if (bot == null)
                continue;

            var newStatus = MapLivekitStreamStatus(result.Status);
            if (newStatus == null)
                continue;
            if (bot.Status == newStatus) continue;

            bot.Status = newStatus.Value;
            if (!string.IsNullOrEmpty(result.Error))
                bot.LastError = result.Error;
            if (newStatus is BroadcastStreambotStatus.Ended
                    or BroadcastStreambotStatus.Failed
                && bot.EndedAt == null)
            {
                bot.EndedAt = now;
            }
            changed.Add(bot);
        }

        if (changed.Count == 0)
            return;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var bot in changed)
        {
            await notificationService.NotifyConversationAsync(
                broadcast.Channel.ConversationId,
                "Broadcast_StreambotStatusChanged",
                new
                {
                    broadcastId = broadcast.Id,
                    channelId = broadcast.ChannelId,
                    streambotId = bot.StreamBotId,
                    status = bot.Status.ToString(),
                    lastError = bot.LastError
                }, ct);
        }
    }

    private static BroadcastStreambotStatus? MapLivekitStreamStatus(string? livekitStatus) =>
        livekitStatus?.ToUpperInvariant() switch
        {
            "ACTIVE" => BroadcastStreambotStatus.Active,
            "FAILED" => BroadcastStreambotStatus.Failed,
            "ABORTED" => BroadcastStreambotStatus.Ended,
            "COMPLETED" => BroadcastStreambotStatus.Ended,
            _ => null
        };

    private sealed class EgressEvent
    {
        public string? Event { get; set; }
        public EgressInfo? EgressInfo { get; set; }
    }

    private sealed class EgressInfo
    {
        public string? EgressId { get; set; }
        public string? Status { get; set; }
        public string? Error { get; set; }
        public List<EgressStreamResult>? StreamResults { get; set; }
    }

    private sealed class EgressStreamResult
    {
        public string? Url { get; set; }
        public string? Status { get; set; }
        public string? Error { get; set; }
    }
}
