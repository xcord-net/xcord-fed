using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Xcord.Features.Bots.Interactions;

public sealed record InteractionCallbackCommand(
    string InteractionToken,
    InteractionCallbackType Type,
    string? Content
);

public sealed record InteractionCallbackRequest(
    InteractionCallbackType Type,
    string? Content
);

public sealed record InteractionCallbackResponse(string Status);

public enum InteractionCallbackType
{
    /// <summary>
    /// Respond immediately with a message.
    /// </summary>
    ChannelMessage = 1,

    /// <summary>
    /// Acknowledge the interaction and respond later (within 15 minutes).
    /// </summary>
    DeferredChannelMessage = 2,

    /// <summary>
    /// Update the original message that contained the component.
    /// </summary>
    UpdateMessage = 3,

    /// <summary>
    /// Acknowledge a component interaction without any visible response.
    /// </summary>
    DeferredUpdateMessage = 4
}

public sealed class InteractionCallbackHandler(
    AppDbContext dbContext,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions,
    IHttpContextAccessor httpContextAccessor,
    IOutboxWriter outboxWriter,
    SnowflakeIdGenerator snowflakeGenerator)
    : IRequestHandler<InteractionCallbackCommand, Result<InteractionCallbackResponse>>
{
    private const string InteractionKeyPrefix = "interaction:";
    private readonly string _redisPrefix = redisOptions.Value.ChannelPrefix;

    private static readonly JsonSerializerOptions TokenDeserializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<Result<InteractionCallbackResponse>> Handle(InteractionCallbackCommand request, CancellationToken ct)
    {
        // Authenticate the bot calling this endpoint.
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var botUserId))
            return Error.Forbidden("UNAUTHORIZED", "Bot is not authenticated");

        // Retrieve the pending interaction from Redis.
        var db = redis.GetDatabase();
        var redisKey = BuildRedisKey(request.InteractionToken);
        var tokenJson = await db.StringGetAsync(redisKey);

        if (tokenJson.IsNullOrEmpty)
            return Error.NotFound("INTERACTION_TOKEN_EXPIRED", "Interaction token has expired or does not exist");

        PendingInteraction? pending;
        try
        {
            pending = JsonSerializer.Deserialize<PendingInteraction>((string)tokenJson!, TokenDeserializerOptions);
        }
        catch
        {
            return Error.Validation("INTERACTION_TOKEN_INVALID", "Interaction token payload is malformed");
        }

        if (pending == null)
            return Error.Validation("INTERACTION_TOKEN_INVALID", "Interaction token payload is malformed");

        // Verify the bot owns this interaction — the token was issued for a specific bot token.
        var botToken = await dbContext.BotTokens.AsNoTracking()
            .Where(bt => bt.UserId == botUserId && bt.Id == pending.BotTokenId && !bt.IsRevoked)
            .FirstOrDefaultAsync(ct);

        if (botToken == null)
            return Error.Forbidden("BOT_MISMATCH", "Bot does not own this interaction");

        // Delete the token immediately so it can only be used once.
        await db.KeyDeleteAsync(redisKey);

        // If the callback type is a deferred update, nothing further to do.
        if (request.Type is InteractionCallbackType.DeferredChannelMessage or InteractionCallbackType.DeferredUpdateMessage)
        {
            return new InteractionCallbackResponse("deferred");
        }

        // For immediate responses, post a follow-up message to the conversation.
        if (!string.IsNullOrEmpty(request.Content))
        {
            var message = new Message
            {
                Id = snowflakeGenerator.NextId(),
                ConversationId = pending.ConversationId,
                AuthorId = botUserId,
                Type = MessageType.Default,
                Content = request.Content,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Messages.Add(message);

            await outboxWriter.WriteAsync(dbContext, "Message.Created", new
            {
                messageId = message.Id,
                conversationId = message.ConversationId,
                authorId = message.AuthorId,
                content = message.Content,
                type = message.Type.ToString(),
                createdAt = message.CreatedAt
            }, ct);

            await dbContext.SaveChangesAsync(ct);
        }

        return new InteractionCallbackResponse("ok");
    }

    private string BuildRedisKey(string token) =>
        $"{_redisPrefix}{InteractionKeyPrefix}{token}";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/interactions/{interactionToken}/callback", async (
            string interactionToken,
            [FromBody] InteractionCallbackRequest request,
            IRequestHandler<InteractionCallbackCommand, Result<InteractionCallbackResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new InteractionCallbackCommand(interactionToken, request.Type, request.Content), ct))
        .RequireAuthorization(Policies.Bot)
        .WithName("InteractionCallback").WithTags("Interactions");
}

/// <summary>
/// Data stored in Redis under the interaction token key.
/// </summary>
internal sealed record PendingInteraction(
    long BotTokenId,
    long ConversationId,
    long UserId,
    string EventType,
    DateTimeOffset IssuedAt
);
