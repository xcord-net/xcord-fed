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

public sealed record AnswerCallRequest(long CallId);

public sealed record AnswerCallResponse(string Token, string RoomName);

public sealed class AnswerCallHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ILiveKitService liveKitService,
    IOutboxWriter outboxWriter,
    IOptions<InstanceOptions> instanceOptions,
    IOptions<TierOptions> tierOptions,
    ILogger<AnswerCallHandler> logger) : IRequestHandler<AnswerCallRequest, Result<AnswerCallResponse>>
{
    private readonly InstanceOptions _instanceOptions = instanceOptions.Value;
    private readonly TierOptions _tierOptions = tierOptions.Value;

    public async Task<Result<AnswerCallResponse>> Handle(AnswerCallRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var currentUserId = userIdResult.Value;

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

            // Build tier-based quality constraints for server-side enforcement
            var qualityConstraints = new VideoQualityConstraints
            {
                MaxAudioBitrateKbps = _tierOptions.MaxAudioBitrateKbps,
                MaxVideoBitrateKbps = _tierOptions.MaxVideoBitrateKbps,
                MaxVideoWidth = _tierOptions.MaxVideoWidth,
                MaxVideoHeight = _tierOptions.MaxVideoHeight,
                MaxVideoFps = _tierOptions.MaxVideoFps,
                MaxScreenShareBitrateKbps = _tierOptions.MaxScreenShareBitrateKbps,
                EnableSimulcast = _tierOptions.CanUseSimulcast
            };

            // Generate LiveKit tokens for both users with server-enforced quality limits
            var callerToken = liveKitService.GenerateToken(
                userId: call.CallerId,
                roomName: roomName,
                canPublish: true,
                canSubscribe: true,
                canPublishData: true,
                canScreenShare: true,
                ttl: TimeSpan.FromHours(2),
                qualityConstraints: qualityConstraints);

            var recipientToken = liveKitService.GenerateToken(
                userId: currentUserId,
                roomName: roomName,
                canPublish: true,
                canSubscribe: true,
                canPublishData: true,
                canScreenShare: true,
                ttl: TimeSpan.FromHours(2),
                qualityConstraints: qualityConstraints);

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
