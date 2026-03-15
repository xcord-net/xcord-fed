using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Calls;

public sealed record DeclineCallRequest(long CallId);

public sealed class DeclineCallHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    ILogger<DeclineCallHandler> logger) : IRequestHandler<DeclineCallRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeclineCallRequest request, CancellationToken cancellationToken)
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
            CallId = call.Id,
            DmChannelId = call.DmChannelId,
            CallerId = call.CallerId,
            RecipientId = currentUserId,
            Status = CallStatus.Declined
        };
        await notificationService.NotifyUserAsync(call.CallerId, "Notify_CallEnded", callPayload);
        await notificationService.NotifyUserAsync(currentUserId, "Notify_CallEnded", callPayload);

        logger.LogInformation(
            "User {UserId} declined call {CallId} in DM channel {DmChannelId}",
            currentUserId, call.Id, call.DmChannelId);

        return true;
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
