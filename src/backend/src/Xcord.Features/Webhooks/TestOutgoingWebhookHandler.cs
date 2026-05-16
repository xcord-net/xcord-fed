using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Webhooks;

public sealed record TestOutgoingWebhookCommand(
    long ServerId,
    long WebhookId
);

public sealed record TestOutgoingWebhookResponse(
    bool Success,
    int? HttpStatus,
    string? Error
);

public sealed class TestOutgoingWebhookHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    IEncryptionService encryptionService,
    IHttpClientFactory httpClientFactory,
    ILogger<TestOutgoingWebhookHandler> logger)
    : IRequestHandler<TestOutgoingWebhookCommand, Result<TestOutgoingWebhookResponse>>,
      IValidatable<TestOutgoingWebhookCommand>
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Error? Validate(TestOutgoingWebhookCommand request)
    {
        if (request.ServerId <= 0)
            return Error.Validation("VALIDATION_ERROR", "ServerId must be positive");

        if (request.WebhookId <= 0)
            return Error.Validation("VALIDATION_ERROR", "WebhookId must be positive");

        return null;
    }

    public async Task<Result<TestOutgoingWebhookResponse>> Handle(
        TestOutgoingWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify server exists
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        // Check ManageWebhooks permission
        var permissionResult = await roleService.EnsureServerRole(
            userId, request.ServerId, Role.ManageWebhooks);

        if (permissionResult.IsFailure)
            return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission to manage webhooks in this server");

        // Load the webhook
        var webhook = await dbContext.OutgoingWebhooks
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WebhookId && w.ServerId == request.ServerId, cancellationToken);

        if (webhook == null)
            return Error.NotFound("WEBHOOK_NOT_FOUND", "Outgoing webhook not found");

        // Build the test ping payload
        var pingPayload = new
        {
            type = "ping",
            webhookId = request.WebhookId.ToString(),
            serverId = request.ServerId.ToString(),
            timestamp = DateTimeOffset.UtcNow
        };

        var payloadJson = JsonSerializer.Serialize(pingPayload, PayloadSerializerOptions);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        // Decrypt the HMAC secret
        string secretHex;
        try
        {
            secretHex = encryptionService.Decrypt(webhook.Secret);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt secret for webhook {WebhookId}", request.WebhookId);
            return new TestOutgoingWebhookResponse(Success: false, HttpStatus: null, Error: "Failed to decrypt webhook secret");
        }

        // Compute HMAC-SHA256 signature
        var secretBytes = Convert.FromHexString(secretHex);
        using var hmac = new HMACSHA256(secretBytes);
        var signatureBytes = hmac.ComputeHash(payloadBytes);
        var signatureHex = Convert.ToHexString(signatureBytes).ToLowerInvariant();

        // SSRF validation: reject private/local target URLs
        if (Uri.TryCreate(webhook.TargetUrl, UriKind.Absolute, out var targetUri))
        {
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(targetUri.Host, cancellationToken).ConfigureAwait(false);
                foreach (var ip in hostEntry.AddressList)
                {
                    if (SsrfSafeHttpClient.IsPrivateOrLocalIp(ip))
                    {
                        logger.LogWarning("SSRF blocked: webhook {WebhookId} targets private IP {Ip}", request.WebhookId, ip);
                        return new TestOutgoingWebhookResponse(
                            Success: false, HttpStatus: null,
                            Error: $"Target URL resolves to a private or local IP address: {ip}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new TestOutgoingWebhookResponse(
                    Success: false, HttpStatus: null,
                    Error: $"Failed to resolve target URL hostname: {ex.Message}");
            }
        }

        // Send the test payload
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new ByteArrayContent(payloadBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, webhook.TargetUrl);
            httpRequest.Content = content;
            httpRequest.Headers.Add("X-Xcord-Signature", $"sha256={signatureHex}");
            httpRequest.Headers.Add("X-Xcord-Event", "ping");
            httpRequest.Headers.Add("X-Xcord-Delivery", request.WebhookId.ToString());

            using var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var isSuccess = response.IsSuccessStatusCode;
            var statusCode = (int)response.StatusCode;

            logger.LogInformation(
                "Test delivery for webhook {WebhookId} returned HTTP {StatusCode}",
                request.WebhookId, statusCode);

            return new TestOutgoingWebhookResponse(
                Success: isSuccess,
                HttpStatus: statusCode,
                Error: isSuccess ? null : $"Remote returned HTTP {statusCode}"
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Test delivery for webhook {WebhookId} failed with exception", request.WebhookId);
            return new TestOutgoingWebhookResponse(Success: false, HttpStatus: null, Error: ex.Message);
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPut("/api/v1/servers/{serverId}/outgoing-webhooks/{webhookId}/test", async (
            long serverId,
            long webhookId,
            IRequestHandler<TestOutgoingWebhookCommand, Result<TestOutgoingWebhookResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new TestOutgoingWebhookCommand(ServerId: serverId, WebhookId: webhookId);
            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAuthorization(Policies.User)
        .WithName("TestOutgoingWebhook")
        .WithTags("OutgoingWebhooks");
    }
}
