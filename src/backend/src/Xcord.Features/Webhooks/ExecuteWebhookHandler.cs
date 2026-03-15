using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Webhooks;

public sealed record ExecuteWebhookCommand(
    long WebhookId,
    string Token,
    string Content,
    string? Username,
    string? AvatarUrl
);

public sealed record ExecuteWebhookResponse(
    long MessageId,
    long ConversationId,
    string Content,
    DateTimeOffset CreatedAt
);

public sealed class ExecuteWebhookHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IConnectionMultiplexer redis,
    INotificationService notificationService,
    ILogger<ExecuteWebhookHandler> logger,
    IOptions<RedisOptions> redisOptions)
    : IRequestHandler<ExecuteWebhookCommand, Result<ExecuteWebhookResponse>>, IValidatable<ExecuteWebhookCommand>
{
    private readonly string _channelPrefix = redisOptions.Value.ChannelPrefix;

    public Error? Validate(ExecuteWebhookCommand request)
    {
        if (request.WebhookId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "WebhookId must be positive");
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Error.Validation("VALIDATION_ERROR", "Token is required");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Error.Validation("VALIDATION_ERROR", "Content is required");
        }

        if (request.Content.Length > 4000)
        {
            return Error.Validation("VALIDATION_ERROR", "Content must be 4000 characters or less");
        }

        if (request.Username != null && request.Username.Length > 80)
        {
            return Error.Validation("VALIDATION_ERROR", "Username must be 80 characters or less");
        }

        if (request.AvatarUrl != null && request.AvatarUrl.Length > 512)
        {
            return Error.Validation("VALIDATION_ERROR", "AvatarUrl must be 512 characters or less");
        }

        return null;
    }

    public async Task<Result<ExecuteWebhookResponse>> Handle(ExecuteWebhookCommand request, CancellationToken cancellationToken)
    {
        // Get webhook and verify token
        var webhook = await dbContext.Webhooks
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WebhookId, cancellationToken);

        if (webhook == null)
        {
            return Error.NotFound("WEBHOOK_NOT_FOUND", "Webhook not found");
        }

        // Verify token matches (constant-time comparison to prevent timing attacks)
        var webhookTokenBytes = Encoding.UTF8.GetBytes(webhook.Token);
        var requestTokenBytes = Encoding.UTF8.GetBytes(request.Token);
        if (!CryptographicOperations.FixedTimeEquals(webhookTokenBytes, requestTokenBytes))
        {
            return Error.Forbidden("INVALID_TOKEN", "Invalid webhook token");
        }

        // Check rate limit: 30 messages per minute per webhook
        var rateLimitKey = $"{_channelPrefix}:webhook:rate:{request.WebhookId}";
        var db = redis.GetDatabase();

        var currentCount = await db.StringIncrementAsync(rateLimitKey);
        if (currentCount == 1)
        {
            // First request in this window, set expiry
            await db.KeyExpireAsync(rateLimitKey, TimeSpan.FromMinutes(1));
        }

        if (currentCount > 30)
        {
            return Error.RateLimited("RATE_LIMIT_EXCEEDED", "Webhook rate limit exceeded (30 messages per minute)");
        }

        // Get channel to resolve conversation ID
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == webhook.ChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        var now = DateTimeOffset.UtcNow;

        // HTML-encode content to prevent XSS (webhook messages bypass MessageProcessor pipeline)
        var sanitizedContent = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(request.Content);

        // Create message entity
        var messageId = snowflakeGenerator.NextId();

        // Build metadata with webhook info
        var metadata = new
        {
            webhookId = webhook.Id,
            webhookName = webhook.Name,
            webhookAvatar = webhook.AvatarUrl,
            username = request.Username,
            avatarUrl = request.AvatarUrl
        };

        var message = new Message
        {
            Id = messageId,
            ConversationId = channel.ConversationId,
            AuthorId = null, // Webhook messages have no author
            Type = MessageType.Default,
            Content = sanitizedContent,
            Metadata = JsonSerializer.Serialize(metadata),
            IsPinned = false,
            CreatedAt = now
        };

        // Begin transaction
        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            dbContext.Messages.Add(message);

            // Update read states: increment UnreadCount for all users
            var readStatesToUpdate = await dbContext.ReadStates
                .Where(rs => rs.ConversationId == channel.ConversationId)
                .ToListAsync(cancellationToken);

            foreach (var readState in readStatesToUpdate)
            {
                readState.UnreadCount++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await notificationService.NotifyConversationAsync(message.ConversationId, "Chat_MessageCreated", new
        {
            MessageId = message.Id,
            ConversationId = message.ConversationId,
            AuthorId = (long?)null
        });

        logger.LogInformation(
            "Webhook {WebhookId} posted message {MessageId} to conversation {ConversationId}",
            webhook.Id, messageId, channel.ConversationId);

        return new ExecuteWebhookResponse(
            MessageId: message.Id,
            ConversationId: message.ConversationId,
            Content: sanitizedContent,
            CreatedAt: message.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/webhooks/{webhookId}/{token}", async (
            long webhookId,
            string token,
            ExecuteWebhookRequest request,
            IRequestHandler<ExecuteWebhookCommand, Result<ExecuteWebhookResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new ExecuteWebhookCommand(
                WebhookId: webhookId,
                Token: token,
                Content: request.Content,
                Username: request.Username,
                AvatarUrl: request.AvatarUrl
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .AllowAnonymous()
        .WithName("ExecuteWebhook")
        .WithTags("Webhooks");
    }
}

public sealed record ExecuteWebhookRequest(
    string Content,
    string? Username,
    string? AvatarUrl
);
