using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Calls;

public sealed record AnswerCallRequest(long CallId);

public sealed record AnswerCallResponse(string Token, string RoomName);

public sealed class AnswerCallHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ILiveKitService liveKitService,
    IOutboxWriter outboxWriter,
    IOptions<InstanceOptions> instanceOptions,
    ILogger<AnswerCallHandler> logger) : IRequestHandler<AnswerCallRequest, Result<AnswerCallResponse>>
{
    private readonly InstanceOptions _instanceOptions = instanceOptions.Value;

    public async Task<Result<AnswerCallResponse>> Handle(AnswerCallRequest request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var currentUserId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Find the call
        var call = await dbContext.Calls
            .Include(c => c.DmChannel)
                .ThenInclude(dm => dm.Members)
            .Where(c => c.Id == request.CallId)
            .FirstOrDefaultAsync(cancellationToken);

        if (call == null)
        {
            return Error.NotFound("CALL_NOT_FOUND", "Call not found");
        }

        // Verify the call is in Ringing status
        if (call.Status != CallStatus.Ringing)
        {
            return Error.BadRequest("INVALID_CALL_STATUS", "Call is not in ringing status");
        }

        // Verify the current user is the recipient (not the caller)
        if (call.CallerId == currentUserId)
        {
            return Error.BadRequest("CANNOT_ANSWER_OWN_CALL", "You cannot answer your own call");
        }

        // Verify the current user is a member of the DM
        if (!call.DmChannel.Members.Any(m => m.UserId == currentUserId))
        {
            return Error.Forbidden("FORBIDDEN", "You are not a member of this DM channel");
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Update call status to Active
            call.Status = CallStatus.Active;
            call.AnsweredAt = DateTimeOffset.UtcNow;

            // Generate LiveKit room name
            var domain = _instanceOptions.Domain;
            var roomName = $"{domain}:call:{call.Id}";

            // Generate LiveKit tokens for both users
            var callerToken = liveKitService.GenerateToken(
                userId: call.CallerId,
                roomName: roomName,
                canPublish: true,
                canSubscribe: true,
                canPublishData: true,
                canScreenShare: true,
                ttl: TimeSpan.FromHours(2));

            var recipientToken = liveKitService.GenerateToken(
                userId: currentUserId,
                roomName: roomName,
                canPublish: true,
                canSubscribe: true,
                canPublishData: true,
                canScreenShare: true,
                ttl: TimeSpan.FromHours(2));

            // Write outbox event to notify the caller
            await outboxWriter.WriteAsync(dbContext, "Call.Answered", new
            {
                CallId = call.Id,
                DmChannelId = call.DmChannelId,
                CallerId = call.CallerId,
                RecipientId = currentUserId,
                CallerToken = callerToken,
                RoomName = roomName
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "User {UserId} answered call {CallId} in DM channel {DmChannelId}",
                currentUserId, call.Id, call.DmChannelId);

            // Return the recipient's token
            return new AnswerCallResponse(recipientToken, roomName);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/calls/{callId}/answer", async (
            long callId,
            [FromServices] AnswerCallHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new AnswerCallRequest(callId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("AnswerCall")
        .WithTags("Calls");
}
