using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Forwards bot interaction events (slash commands, component interactions) to the bot's
/// configured InteractionEndpointUrl via HTTP POST, signed with HMAC-SHA256.
/// An interaction token is issued and stored in Redis (TTL 15 minutes) so the bot can
/// POST a callback to /api/v1/interactions/{token}/callback.
/// Called from the event dispatch pipeline when a Bot_* outbox event is processed.
/// </summary>
public sealed class BotInteractionForwarder
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IEncryptionService _encryptionService;
    private readonly IConnectionMultiplexer _redis;
    private readonly string _redisPrefix;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BotInteractionForwarder> _logger;

    private const string InteractionKeyPrefix = "interaction:";
    private static readonly TimeSpan InteractionTokenTtl = TimeSpan.FromMinutes(15);

    // Serializer options for the Redis token payload — plain camelCase, no custom converters needed.
    private static readonly JsonSerializerOptions TokenSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BotInteractionForwarder(
        IServiceScopeFactory serviceScopeFactory,
        IEncryptionService encryptionService,
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<BotInteractionForwarder> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _encryptionService = encryptionService;
        _redis = redis;
        _redisPrefix = redisOptions.Value.ChannelPrefix;
        _httpClient = httpClientFactory.CreateClient("BotInteraction");
        _logger = logger;
    }

    /// <summary>
    /// Attempts to forward the bot interaction payload to the bot's registered endpoint.
    /// Returns immediately if the bot has no endpoint configured.
    /// Throws on delivery failure so the outbox can retry.
    /// </summary>
    public async Task ForwardAsync(string eventType, string payload, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        // Extract botTokenId — both command and interaction events carry this field.
        if (!root.TryGetProperty("botTokenId", out var botTokenIdElement))
        {
            _logger.LogWarning("Bot event {EventType} payload missing botTokenId, skipping forwarding", eventType);
            return;
        }

        long botTokenId;
        if (botTokenIdElement.ValueKind == JsonValueKind.Number)
        {
            if (!botTokenIdElement.TryGetInt64(out botTokenId))
            {
                _logger.LogWarning("Bot event {EventType} botTokenId is not a valid long, skipping forwarding", eventType);
                return;
            }
        }
        else if (botTokenIdElement.ValueKind == JsonValueKind.String)
        {
            var str = botTokenIdElement.GetString();
            if (!long.TryParse(str, out botTokenId))
            {
                _logger.LogWarning("Bot event {EventType} botTokenId is not a valid long, skipping forwarding", eventType);
                return;
            }
        }
        else if (botTokenIdElement.ValueKind == JsonValueKind.Null)
        {
            // Component interaction from a non-bot message author — nothing to forward.
            _logger.LogDebug("Bot event {EventType} has null botTokenId, skipping forwarding", eventType);
            return;
        }
        else
        {
            _logger.LogWarning(
                "Bot event {EventType} botTokenId has unexpected kind {Kind}, skipping forwarding",
                eventType, botTokenIdElement.ValueKind);
            return;
        }

        // Extract conversationId and userId for the interaction token payload.
        long conversationId = 0;
        long userId = 0;
        TryExtractLong(root, "conversationId", out conversationId);
        TryExtractLong(root, "userId", out userId);

        // Look up the bot token's endpoint configuration.
        string? endpointUrl;
        byte[]? encryptedSigningKey;

        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var botToken = await dbContext.BotTokens.AsNoTracking()
                .Where(bt => bt.Id == botTokenId)
                .Select(bt => new { bt.InteractionEndpointUrl, bt.InteractionSigningKey, bt.IsRevoked })
                .FirstOrDefaultAsync(cancellationToken);

            if (botToken == null)
            {
                _logger.LogWarning(
                    "Bot event {EventType}: BotToken {BotTokenId} not found, skipping forwarding",
                    eventType, botTokenId);
                return;
            }

            if (botToken.IsRevoked)
            {
                _logger.LogDebug(
                    "Bot event {EventType}: BotToken {BotTokenId} is revoked, skipping forwarding",
                    eventType, botTokenId);
                return;
            }

            if (string.IsNullOrEmpty(botToken.InteractionEndpointUrl))
            {
                // Bot does not use webhook delivery (it polls instead). Silently skip.
                return;
            }

            endpointUrl = botToken.InteractionEndpointUrl;
            encryptedSigningKey = botToken.InteractionSigningKey;
        }

        // Issue an interaction token stored in Redis so the bot can POST a callback.
        var interactionToken = GenerateInteractionToken();
        var pendingPayload = JsonSerializer.Serialize(new
        {
            botTokenId,
            conversationId,
            userId,
            eventType,
            issuedAt = DateTimeOffset.UtcNow
        }, TokenSerializerOptions);

        var db = _redis.GetDatabase();
        var redisKey = $"{_redisPrefix}{InteractionKeyPrefix}{interactionToken}";
        await db.StringSetAsync(redisKey, pendingPayload, InteractionTokenTtl);

        // Build the outgoing payload by augmenting the original with the interaction token.
        // We embed the token so the bot knows which token to use for its callback.
        string enrichedPayload;
        using (var outputStream = new System.IO.MemoryStream())
        using (var writer = new Utf8JsonWriter(outputStream))
        {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject())
            {
                property.WriteTo(writer);
            }
            writer.WriteString("interactionToken", interactionToken);
            writer.WriteEndObject();
            writer.Flush();
            enrichedPayload = Encoding.UTF8.GetString(outputStream.ToArray());
        }

        // Determine interaction type header value.
        var interactionType = eventType switch
        {
            "Bot_CommandExecuted" => "command",
            "Bot_Interaction" => "component",
            _ => eventType.ToLowerInvariant()
        };

        // Build the request.
        using var content = new StringContent(enrichedPayload, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = content
        };
        request.Headers.Add("X-Xcord-Interaction-Type", interactionType);

        // Sign with HMAC-SHA256 if a signing key is configured.
        if (encryptedSigningKey != null && encryptedSigningKey.Length > 0)
        {
            var signingKeyBytes = DecryptSigningKey(encryptedSigningKey);
            var signature = ComputeHmacSignature(enrichedPayload, signingKeyBytes);
            request.Headers.Add("X-Xcord-Bot-Signature", $"sha256={signature}");
        }

        _logger.LogInformation(
            "Forwarding {EventType} interaction to bot endpoint {Url} with token {Token}",
            eventType, MaskUrl(endpointUrl), interactionToken[..8] + "...");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // Clean up the Redis token since the delivery failed.
            await db.KeyDeleteAsync(redisKey);

            // Throw so the outbox dispatcher increments the retry counter.
            throw new InvalidOperationException(
                $"Bot interaction delivery failed for event {eventType} to {MaskUrl(endpointUrl)}: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;

            // 4xx errors (except 429) are not retryable — log and return to avoid infinite retry.
            if (status >= 400 && status < 500 && status != 429)
            {
                _logger.LogError(
                    "Bot interaction endpoint returned non-retryable {StatusCode} for event {EventType} to {Url}",
                    status, eventType, MaskUrl(endpointUrl));
                await db.KeyDeleteAsync(redisKey);
                return;
            }

            // 5xx and 429 are retryable — delete the token and throw so the outbox backs off.
            await db.KeyDeleteAsync(redisKey);
            throw new InvalidOperationException(
                $"Bot interaction delivery returned {status} for event {eventType} to {MaskUrl(endpointUrl)}");
        }

        _logger.LogInformation(
            "Successfully delivered {EventType} interaction to bot endpoint (HTTP {StatusCode})",
            eventType, (int)response.StatusCode);
    }

    private byte[] DecryptSigningKey(byte[] encryptedSigningKey)
    {
        var rawKeyBase64 = _encryptionService.Decrypt(encryptedSigningKey);
        return Convert.FromBase64String(rawKeyBase64);
    }

    private static string ComputeHmacSignature(string payload, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GenerateInteractionToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        // URL-safe base64 without padding
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static bool TryExtractLong(JsonElement root, string propertyName, out long value)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            value = 0;
            return false;
        }
        if (element.ValueKind == JsonValueKind.Number)
            return element.TryGetInt64(out value);
        if (element.ValueKind == JsonValueKind.String)
            return long.TryParse(element.GetString(), out value);
        value = 0;
        return false;
    }

    /// <summary>
    /// Masks the URL path in logs to avoid leaking full endpoint paths with secrets.
    /// </summary>
    private static string MaskUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Host}/***";
        }
        catch
        {
            return "[invalid-url]";
        }
    }
}
