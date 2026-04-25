using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Calls;

public sealed record InitiateCallRequest(long DmChannelId);

public sealed record InitiateCallResponse(long CallId, long DmChannelId);

public sealed class InitiateCallHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions,
    ILogger<InitiateCallHandler> logger) : IRequestHandler<InitiateCallRequest, Result<InitiateCallResponse>>
{
    private readonly string _channelPrefix = redisOptions.Value.ChannelPrefix;

    public async Task<Result<InitiateCallResponse>> Handle(InitiateCallRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var currentUserId = userIdResult.Value;

        // Verify DM channel exists and user is a member
        var dmChannel = await dbContext.DmChannels
            .Include(dm => dm.Members)
            .Where(dm => dm.Id == request.DmChannelId)
            .FirstOrDefaultAsync(cancellationToken);

        if (dmChannel == null)
        {
            return Error.NotFound("DM_CHANNEL_NOT_FOUND", "DM channel not found");
        }

        // Verify user is a member of the DM
        if (!dmChannel.Members.Any(m => m.UserId == currentUserId))
        {
            return Error.Forbidden("FORBIDDEN", "You are not a member of this DM channel");
        }

        // Verify this is a 1:1 DM (not a group DM)
        if (dmChannel.IsGroup)
        {
            return Error.Validation("INVALID_DM_TYPE", "Calls are only supported in 1:1 DMs");
        }

        // Check if there's already an active or ringing call for this DM
        var existingActiveCall = await dbContext.Calls
            .Where(c => c.DmChannelId == request.DmChannelId)
            .Where(c => c.Status == CallStatus.Ringing || c.Status == CallStatus.Active)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingActiveCall != null)
        {
            return Error.Conflict("CALL_IN_PROGRESS", "There is already an active or ringing call in this DM");
        }

        // Get the recipient (the other user in the DM)
        var recipientId = dmChannel.Members.First(m => m.UserId != currentUserId).UserId;

        var minId = Math.Min(currentUserId, recipientId);
        var maxId = Math.Max(currentUserId, recipientId);
        var rateLimitKey = $"{_channelPrefix}:callinit:{minId}:{maxId}";
        var redisDb = redis.GetDatabase();
        var acquired = await redisDb.StringSetAsync(
            rateLimitKey,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            TimeSpan.FromSeconds(10),
            When.NotExists);

        if (!acquired)
        {
            return Error.RateLimited("CALL_RATE_LIMITED", "Please wait before placing another call");
        }

        var callId = snowflakeGenerator.NextId();

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var now = DateTimeOffset.UtcNow;

            var call = new Call
            {
                Id = callId,
                DmChannelId = request.DmChannelId,
                CallerId = currentUserId,
                Status = CallStatus.Ringing,
                StartedAt = now,
                AnsweredAt = null,
                EndedAt = null
            };

            dbContext.Calls.Add(call);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // Notify both participants after the transaction is committed
        var callPayload = new
        {
            CallId = callId,
            DmChannelId = request.DmChannelId,
            CallerId = currentUserId,
            RecipientId = recipientId
        };
        await notificationService.NotifyUserAsync(currentUserId, "Notify_IncomingCall", callPayload);
        await notificationService.NotifyUserAsync(recipientId, "Notify_IncomingCall", callPayload);

        logger.LogInformation(
            "User {UserId} initiated call {CallId} in DM channel {DmChannelId}",
            currentUserId, callId, request.DmChannelId);

        return new InitiateCallResponse(callId, request.DmChannelId);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/users/@me/dms/{dmChannelId}/call", async (
            long dmChannelId,
            [FromServices] InitiateCallHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new InitiateCallRequest(dmChannelId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("InitiateCall")
        .WithTags("Calls");
}
