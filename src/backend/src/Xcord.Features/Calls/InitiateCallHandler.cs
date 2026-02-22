using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Calls;

public sealed record InitiateCallRequest(long DmChannelId);

public sealed record InitiateCallResponse(long CallId, long DmChannelId);

public sealed class InitiateCallHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor,
    IOutboxWriter outboxWriter,
    ILogger<InitiateCallHandler> logger) : IRequestHandler<InitiateCallRequest, Result<InitiateCallResponse>>
{
    public async Task<Result<InitiateCallResponse>> Handle(InitiateCallRequest request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var currentUserId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

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

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Create the call
            var callId = snowflakeGenerator.NextId();
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

            // Write outbox event to notify the recipient
            await outboxWriter.WriteAsync(dbContext, "Call.Initiated", new
            {
                CallId = callId,
                DmChannelId = request.DmChannelId,
                CallerId = currentUserId,
                RecipientId = recipientId
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "User {UserId} initiated call {CallId} in DM channel {DmChannelId}",
                currentUserId, callId, request.DmChannelId);

            return new InitiateCallResponse(callId, request.DmChannelId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
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
