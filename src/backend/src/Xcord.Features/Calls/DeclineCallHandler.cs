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

public sealed record DeclineCallRequest(long CallId);

public sealed class DeclineCallHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IOutboxWriter outboxWriter,
    ILogger<DeclineCallHandler> logger) : IRequestHandler<DeclineCallRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeclineCallRequest request, CancellationToken cancellationToken)
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
            return Error.BadRequest("CANNOT_DECLINE_OWN_CALL", "You cannot decline your own call");
        }

        // Verify the current user is a member of the DM
        if (!call.DmChannel.Members.Any(m => m.UserId == currentUserId))
        {
            return Error.Forbidden("FORBIDDEN", "You are not a member of this DM channel");
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Update call status to Declined
            call.Status = CallStatus.Declined;
            call.EndedAt = DateTimeOffset.UtcNow;

            // Write outbox event to notify the caller
            await outboxWriter.WriteAsync(dbContext, "Call.Ended", new
            {
                CallId = call.Id,
                DmChannelId = call.DmChannelId,
                CallerId = call.CallerId,
                RecipientId = currentUserId,
                Status = CallStatus.Declined
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "User {UserId} declined call {CallId} in DM channel {DmChannelId}",
                currentUserId, call.Id, call.DmChannelId);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/calls/{callId}/decline", async (
            long callId,
            [FromServices] DeclineCallHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new DeclineCallRequest(callId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeclineCall")
        .WithTags("Calls");
}
