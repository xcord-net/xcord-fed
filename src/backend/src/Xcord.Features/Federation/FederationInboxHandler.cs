using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Federation;

public sealed record FederationInboxRequest(
    string SourceInstanceUrl,
    string SourceChannelId,
    FederationInboxMessageDto[] Messages
);

public sealed record FederationInboxResponse(int Accepted, int Rejected);

public sealed class FederationInboxHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    INotificationService notificationService,
    ILogger<FederationInboxHandler> logger)
    : IRequestHandler<FederationInboxRequest, Result<FederationInboxResponse>>, IValidatable<FederationInboxRequest>
{
    public Error? Validate(FederationInboxRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceInstanceUrl))
            return Error.Validation("VALIDATION_FAILED", "SourceInstanceUrl is required");

        if (string.IsNullOrWhiteSpace(request.SourceChannelId))
            return Error.Validation("VALIDATION_FAILED", "SourceChannelId is required");

        if (request.Messages == null || request.Messages.Length == 0)
            return Error.Validation("VALIDATION_FAILED", "At least one message is required");

        if (request.Messages.Length > 50)
            return Error.Validation("VALIDATION_FAILED", "Maximum 50 messages per inbox delivery");

        return null;
    }

    public async Task<Result<FederationInboxResponse>> Handle(FederationInboxRequest request, CancellationToken cancellationToken)
    {
        var normalizedUrl = request.SourceInstanceUrl.TrimEnd('/');

        // Find all active follows for this remote channel
        var follows = await dbContext.FederationFollows
            .AsNoTracking()
            .Where(f => f.RemoteInstanceUrl == normalizedUrl
                     && f.RemoteChannelId == request.SourceChannelId
                     && f.IsActive)
            .Include(f => f.LocalChannel)
            .ToListAsync(cancellationToken);

        if (follows.Count == 0)
        {
            // No active follows means this source isn't authorized to deliver here.
            return Error.NotFound("NO_FOLLOWS", "No active follows for this remote channel");
        }

        // Batch dedup: collect all remote message IDs and query once per follow
        var remoteMessageIds = request.Messages.Select(m => m.RemoteMessageId).ToList();
        var followIds = follows.Select(f => f.Id).ToList();

        var alreadyImportedSet = (await dbContext.FederationMessages
            .Where(fm => followIds.Contains(fm.FederationFollowId)
                      && remoteMessageIds.Contains(fm.RemoteMessageId))
            .Select(fm => new { fm.FederationFollowId, fm.RemoteMessageId })
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var accepted = 0;
        var rejected = 0;

        // Collect (localMessageId, conversationId) pairs to notify after save
        var pendingNotifications = new List<(long LocalMessageId, long ConversationId)>();

        foreach (var message in request.Messages)
        {
            try
            {
                foreach (var follow in follows)
                {
                    if (alreadyImportedSet.Contains(new { FederationFollowId = follow.Id, RemoteMessageId = message.RemoteMessageId }))
                        continue;

                    // Create local message copy
                    var localMessageId = snowflakeGenerator.NextId();
                    // HTML-encode content to prevent XSS from remote instances
                    var sanitizedContent = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(message.Content);
                    var localMessage = new Message
                    {
                        Id = localMessageId,
                        ConversationId = follow.LocalChannel.ConversationId,
                        AuthorId = null, // System/federated message
                        Type = MessageType.Default,
                        Content = sanitizedContent,
                        Metadata = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            federated = true,
                            sourceInstance = normalizedUrl,
                            sourceChannelId = request.SourceChannelId,
                            remoteMessageId = message.RemoteMessageId,
                            remoteAuthorName = message.AuthorName,
                            remoteAuthorAvatarUrl = message.AuthorAvatarUrl
                        }),
                        CreatedAt = message.CreatedAt
                    };

                    dbContext.Messages.Add(localMessage);

                    // Create federation message record
                    var fedMessage = new FederationMessage
                    {
                        Id = snowflakeGenerator.NextId(),
                        FederationFollowId = follow.Id,
                        RemoteMessageId = message.RemoteMessageId,
                        LocalMessageId = localMessageId,
                        RemoteAuthorName = message.AuthorName,
                        RemoteAuthorAvatarUrl = message.AuthorAvatarUrl,
                        ReceivedAt = DateTimeOffset.UtcNow
                    };

                    dbContext.FederationMessages.Add(fedMessage);

                    pendingNotifications.Add((localMessageId, follow.LocalChannel.ConversationId));
                }

                accepted++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process federated message {RemoteMessageId}", message.RemoteMessageId);
                rejected++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Dispatch SignalR events after save
        foreach (var (localMessageId, conversationId) in pendingNotifications)
        {
            await notificationService.NotifyConversationAsync(conversationId, "Chat_MessageCreated", new
            {
                MessageId = localMessageId,
                ConversationId = conversationId,
                Federated = true
            });
        }

        logger.LogInformation(
            "Federation inbox from {SourceUrl}: {Accepted} accepted, {Rejected} rejected",
            normalizedUrl, accepted, rejected);

        return new FederationInboxResponse(accepted, rejected);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/federation/inbox", async (
            HttpContext httpContext,
            [FromServices] FederationInboxHandler handler,
            [FromServices] IOptions<FederationOptions> federationOptions,
            [FromServices] AppDbContext db,
            [FromServices] IEncryptionService encryptionService,
            CancellationToken ct) =>
        {
            // Read raw body first (before JSON deserialization) for HMAC verification
            using var reader = new StreamReader(httpContext.Request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);

            var request = System.Text.Json.JsonSerializer.Deserialize<FederationInboxRequest>(rawBody,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
                return Results.BadRequest("Invalid request body");

            var signatureHeader = httpContext.Request.Headers["X-Federation-Signature"].FirstOrDefault();
            var requireSignature = federationOptions.Value.RequireSignatureVerification;

            if (requireSignature || !string.IsNullOrEmpty(signatureHeader))
            {
                if (string.IsNullOrEmpty(signatureHeader))
                {
                    return Results.Problem(
                        statusCode: 401,
                        title: "SIGNATURE_REQUIRED",
                        detail: "X-Federation-Signature header is required");
                }

                // Look up the shared secret for this source instance
                var normalizedUrl = request.SourceInstanceUrl?.TrimEnd('/') ?? "";
                var follow = await db.FederationFollows
                    .AsNoTracking()
                    .Where(f => f.RemoteInstanceUrl == normalizedUrl && f.IsActive)
                    .Select(f => new { f.SharedSecret })
                    .FirstOrDefaultAsync(ct);

                if (follow == null || follow.SharedSecret.Length == 0)
                {
                    return Results.Problem(
                        statusCode: 401,
                        title: "UNKNOWN_SOURCE",
                        detail: "No active follow relationship with shared secret for this source instance");
                }

                // Decrypt the shared secret and verify HMAC
                var secretBase64 = encryptionService.Decrypt(follow.SharedSecret);
                var secretBytes = Convert.FromBase64String(secretBase64);
                var verifyError = FederationSignatureService.Verify(secretBytes, rawBody, signatureHeader);

                if (verifyError != null)
                {
                    return Results.Problem(
                        statusCode: 401,
                        title: "INVALID_SIGNATURE",
                        detail: verifyError);
                }
            }
            else
            {
                // Development mode: allow unsigned requests with a warning header
                httpContext.Response.Headers["X-Federation-Warning"] = "Signature verification is disabled";
            }

            return await handler.ExecuteAsync(request, ct);
        })
        .WithName("FederationInbox")
        .WithTags("Federation");
}
