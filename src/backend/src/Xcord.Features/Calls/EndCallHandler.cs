using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Calls;

public sealed record EndCallRequest(long CallId);

public sealed class EndCallHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    ILiveKitService liveKitService,
    IOutboxWriter outboxWriter,
    IOptions<InstanceOptions> instanceOptions,
    ILogger<EndCallHandler> logger) : IRequestHandler<EndCallRequest, Result<bool>>
{
    private readonly InstanceOptions _instanceOptions = instanceOptions.Value;

    public async Task<Result<bool>> Handle(EndCallRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var currentUserId = userIdResult.Value;

        // Find the call
        var call = await dbContext.Calls
            .Include(c => c.DmChannel)
                .ThenInclude(dm => dm.Members)
            .Include(c => c.DmChannel.Conversation)
            .Where(c => c.Id == request.CallId)
            .FirstOrDefaultAsync(cancellationToken);

        if (call == null)
        {
            return Error.NotFound("CALL_NOT_FOUND", "Call not found");
        }

        // Verify the call is in Ringing or Active status
        if (call.Status != CallStatus.Ringing && call.Status != CallStatus.Active)
        {
            return Error.BadRequest("INVALID_CALL_STATUS", "Call is not active or ringing");
        }

        // Verify the current user is either the caller or the recipient
        var recipientId = call.DmChannel.Members.FirstOrDefault(m => m.UserId != call.CallerId)?.UserId;
        if (call.CallerId != currentUserId && recipientId != currentUserId)
        {
            return Error.Forbidden("FORBIDDEN", "You are not a participant in this call");
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var now = DateTimeOffset.UtcNow;

            // Update call status to Ended
            call.Status = CallStatus.Ended;
            call.EndedAt = now;

            // Calculate call duration (only if call was answered)
            TimeSpan? callDuration = null;
            if (call.AnsweredAt.HasValue)
            {
                callDuration = now - call.AnsweredAt.Value;
            }

            // Remove LiveKit participants if call was active
            if (call.Status == CallStatus.Active)
            {
                var domain = _instanceOptions.Domain;
                var roomName = $"{domain}:call:{call.Id}";

                try
                {
                    await liveKitService.RemoveParticipantAsync(roomName, call.CallerId.ToString());
                    if (recipientId.HasValue)
                    {
                        await liveKitService.RemoveParticipantAsync(roomName, recipientId.Value.ToString());
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to remove LiveKit participants for call {CallId}", call.Id);
                }
            }

            // Create system message in DM conversation if call was answered
            if (callDuration.HasValue)
            {
                var messageId = snowflakeGenerator.NextId();
                var message = new Message
                {
                    Id = messageId,
                    ConversationId = call.DmChannel.ConversationId,
                    AuthorId = call.CallerId,
                    Type = MessageType.CallStarted,
                    Content = string.Empty,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        CallId = call.Id,
                        DurationSeconds = (int)callDuration.Value.TotalSeconds
                    })
                };

                dbContext.Messages.Add(message);
            }

            // Write outbox event to notify both users
            await outboxWriter.WriteAsync(dbContext, "Call.Ended", new
            {
                CallId = call.Id,
                DmChannelId = call.DmChannelId,
                CallerId = call.CallerId,
                RecipientId = recipientId,
                Status = CallStatus.Ended,
                DurationSeconds = callDuration.HasValue ? (int)callDuration.Value.TotalSeconds : 0
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "User {UserId} ended call {CallId} in DM channel {DmChannelId} (Duration: {Duration}s)",
                currentUserId, call.Id, call.DmChannelId, callDuration?.TotalSeconds ?? 0);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/calls/{callId}/end", async (
            long callId,
            [FromServices] EndCallHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new EndCallRequest(callId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("EndCall")
        .WithTags("Calls");
}
