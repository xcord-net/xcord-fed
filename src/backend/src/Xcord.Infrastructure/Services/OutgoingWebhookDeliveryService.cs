using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that delivers pending OutgoingWebhookDelivery records to their
/// configured target URLs using HMAC-SHA256 signed HTTP POST requests.
///
/// Retry schedule (AttemptCount → wait before next retry):
///   0 → immediate
///   1 → 1 minute
///   2 → 5 minutes
///   3 → 30 minutes
///   4 → 2 hours
///   5 → 24 hours
///   6 → dead-lettered (no further attempts)
///
/// Polls every 10 seconds. Processes up to 50 deliveries per batch.
/// </summary>
public sealed class OutgoingWebhookDeliveryService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OutgoingWebhookDeliveryService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;
    private const int MaxAttempts = 6; // After this many failures, dead-letter

    /// <summary>
    /// Retry backoff schedule indexed by AttemptCount (number of attempts already made).
    /// Index 0 = after 0 previous attempts → deliver immediately (no delay needed).
    /// </summary>
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.Zero,           // attempt 1 (immediate)
        TimeSpan.FromMinutes(1), // attempt 2 (1 min after first failure)
        TimeSpan.FromMinutes(5), // attempt 3
        TimeSpan.FromMinutes(30),// attempt 4
        TimeSpan.FromHours(2),   // attempt 5
        TimeSpan.FromHours(24),  // attempt 6
    ];

    public OutgoingWebhookDeliveryService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<OutgoingWebhookDeliveryService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutgoingWebhookDeliveryService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outgoing webhook deliveries");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("OutgoingWebhookDeliveryService stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        var now = DateTimeOffset.UtcNow;

        // Find pending or failed deliveries that are due for a delivery attempt
        var deliveries = await dbContext.OutgoingWebhookDeliveries
            .Where(d => (d.Status == OutgoingWebhookDeliveryStatus.Pending
                      || d.Status == OutgoingWebhookDeliveryStatus.Failed)
                     && (d.NextAttemptAt == null || d.NextAttemptAt <= now))
            .OrderBy(d => d.CreatedAt)
            .Take(BatchSize)
            .Include(d => d.Webhook)
            .ToListAsync(cancellationToken);

        if (deliveries.Count == 0)
            return;

        _logger.LogDebug("OutgoingWebhookDeliveryService processing {Count} deliveries", deliveries.Count);

        var httpClient = _httpClientFactory.CreateClient("OutgoingWebhooks");

        foreach (var delivery in deliveries)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await DeliverAsync(delivery, httpClient, encryptionService, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task DeliverAsync(
        OutgoingWebhookDelivery delivery,
        HttpClient httpClient,
        IEncryptionService encryptionService,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var webhook = delivery.Webhook;

        // Skip if webhook is soft-deleted or inactive
        if (webhook.DeletedAt != null || !webhook.IsActive)
        {
            delivery.Status = OutgoingWebhookDeliveryStatus.DeadLettered;
            delivery.LastError = "Webhook is deleted or inactive";
            delivery.LastAttemptAt = now;
            return;
        }

        var payloadBytes = Encoding.UTF8.GetBytes(delivery.Payload);

        // Decrypt the HMAC secret
        string secretHex;
        try
        {
            secretHex = encryptionService.Decrypt(webhook.Secret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt secret for webhook {WebhookId}", webhook.Id);
            delivery.LastError = "Failed to decrypt webhook secret";
            delivery.LastAttemptAt = now;
            delivery.AttemptCount++;
            ScheduleNextAttemptOrDeadLetter(delivery, now);
            return;
        }

        // Compute HMAC-SHA256 signature
        var secretBytes = Convert.FromHexString(secretHex);
        using var hmac = new HMACSHA256(secretBytes);
        var signatureBytes = hmac.ComputeHash(payloadBytes);
        var signatureHex = Convert.ToHexString(signatureBytes).ToLowerInvariant();

        // SSRF protection: resolve hostname and reject private/local IPs before sending
        try
        {
            if (!Uri.TryCreate(webhook.TargetUrl, UriKind.Absolute, out var targetUri)
                || (targetUri.Scheme != "http" && targetUri.Scheme != "https"))
            {
                delivery.LastError = "Invalid target URL or non-HTTP(S) scheme";
                delivery.LastAttemptAt = now;
                delivery.AttemptCount++;
                ScheduleNextAttemptOrDeadLetter(delivery, now);
                return;
            }

            var addresses = await Dns.GetHostAddressesAsync(targetUri.Host);
            foreach (var addr in addresses)
            {
                if (SsrfSafeHttpClient.IsPrivateOrLocalIp(addr))
                {
                    _logger.LogWarning(
                        "SSRF blocked: webhook {WebhookId} target {Url} resolves to private IP {Ip}",
                        webhook.Id, webhook.TargetUrl, addr);
                    delivery.LastError = $"Target URL resolves to a private or local IP address";
                    delivery.LastAttemptAt = now;
                    delivery.Status = OutgoingWebhookDeliveryStatus.DeadLettered;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            delivery.LastError = $"DNS resolution failed: {(ex.Message.Length > 500 ? ex.Message[..500] : ex.Message)}";
            delivery.LastAttemptAt = now;
            delivery.AttemptCount++;
            ScheduleNextAttemptOrDeadLetter(delivery, now);
            return;
        }

        // Attempt the HTTP POST
        try
        {
            using var content = new ByteArrayContent(payloadBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, webhook.TargetUrl);
            request.Content = content;
            request.Headers.Add("X-Xcord-Signature", $"sha256={signatureHex}");
            request.Headers.Add("X-Xcord-Event", delivery.EventType);
            request.Headers.Add("X-Xcord-Delivery", delivery.Id.ToString());

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            using var response = await httpClient.SendAsync(request, cts.Token);
            var statusCode = (int)response.StatusCode;

            delivery.LastAttemptAt = now;
            delivery.LastHttpStatus = statusCode;
            delivery.AttemptCount++;

            if (response.IsSuccessStatusCode)
            {
                delivery.Status = OutgoingWebhookDeliveryStatus.Delivered;
                delivery.LastError = null;

                _logger.LogDebug(
                    "Webhook delivery {DeliveryId} succeeded with HTTP {StatusCode}",
                    delivery.Id, statusCode);
            }
            else
            {
                delivery.LastError = $"Remote returned HTTP {statusCode}";
                ScheduleNextAttemptOrDeadLetter(delivery, now);

                _logger.LogWarning(
                    "Webhook delivery {DeliveryId} failed with HTTP {StatusCode} (attempt {Attempt})",
                    delivery.Id, statusCode, delivery.AttemptCount);
            }
        }
        catch (Exception ex)
        {
            delivery.LastAttemptAt = now;
            delivery.AttemptCount++;
            delivery.LastError = ex.Message.Length > 1000
                ? ex.Message[..1000]
                : ex.Message;

            ScheduleNextAttemptOrDeadLetter(delivery, now);

            _logger.LogWarning(ex,
                "Webhook delivery {DeliveryId} failed with exception (attempt {Attempt})",
                delivery.Id, delivery.AttemptCount);
        }
    }

    private static void ScheduleNextAttemptOrDeadLetter(OutgoingWebhookDelivery delivery, DateTimeOffset now)
    {
        if (delivery.AttemptCount >= MaxAttempts)
        {
            delivery.Status = OutgoingWebhookDeliveryStatus.DeadLettered;
            delivery.NextAttemptAt = null;
        }
        else
        {
            delivery.Status = OutgoingWebhookDeliveryStatus.Failed;
            var delay = delivery.AttemptCount < RetryDelays.Length
                ? RetryDelays[delivery.AttemptCount]
                : RetryDelays[^1];
            delivery.NextAttemptAt = now.Add(delay);
        }
    }
}
