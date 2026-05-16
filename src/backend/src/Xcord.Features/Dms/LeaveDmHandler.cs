using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Dms;

public sealed record LeaveDmRequest(
    long DmChannelId
);

public sealed class LeaveDmHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    ILogger<LeaveDmHandler> logger) : IRequestHandler<LeaveDmRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(LeaveDmRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var currentUserId = userIdResult.Value;

        // Get DM channel
        var dmChannel = await dbContext.DmChannels
            .Include(dm => dm.Members)
            .FirstOrDefaultAsync(dm => dm.Id == request.DmChannelId, cancellationToken);

        if (dmChannel == null)
        {
            return Error.NotFound("DM_NOT_FOUND", "DM channel not found");
        }

        // Find member to remove
        var member = dmChannel.Members.FirstOrDefault(m => m.UserId == currentUserId);
        if (member == null)
        {
            return Error.NotFound("NOT_MEMBER", "You are not a member of this DM channel");
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Capture all member IDs before removing (so the leaving user is notified too)
            var allMemberIds = dmChannel.Members.Select(m => m.UserId).Distinct().ToList();

            // Remove current user from members
            dbContext.DmChannelMembers.Remove(member);

            // For group DMs: transfer ownership if leaving as owner
            if (dmChannel.IsGroup && currentUserId == dmChannel.OwnerId)
            {
                var remainingMembers = dmChannel.Members
                    .Where(m => m.UserId != currentUserId)
                    .OrderBy(m => m.JoinedAt)
                    .ToList();

                if (remainingMembers.Count == 0)
                {
                    // No members left, soft-delete the DM channel
                    dmChannel.SoftDelete();
                    logger.LogInformation(
                        "Group DM {DmChannelId} soft-deleted (all members left)",
                        request.DmChannelId);
                }
                else
                {
                    // Transfer ownership to oldest member
                    dmChannel.OwnerId = remainingMembers[0].UserId;
                    logger.LogInformation(
                        "Group DM {DmChannelId} ownership transferred to user {NewOwnerId}",
                        request.DmChannelId, dmChannel.OwnerId);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Notify all members (including the user who left) after save
            var memberRemovedPayload = new
            {
                DmChannelId = request.DmChannelId,
                UserId = currentUserId,
                RemovedBy = currentUserId
            };
            foreach (var memberId in allMemberIds)
            {
                await notificationService.NotifyUserAsync(memberId, "Notify_DmMemberRemoved", memberRemovedPayload, cancellationToken).ConfigureAwait(false);
            }

            logger.LogInformation(
                "User {UserId} left DM {DmChannelId}",
                currentUserId, request.DmChannelId);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/users/@me/dms/{dmChannelId:long}", async (
            long dmChannelId,
            [FromServices] LeaveDmHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new LeaveDmRequest(dmChannelId), ct,
                onSuccess: _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("LeaveDm")
        .WithTags("DMs");
}
