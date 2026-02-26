using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Dms;

public sealed record RemoveGroupDmMemberRequest(
    long DmChannelId,
    long UserId
);

public sealed class RemoveGroupDmMemberHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IOutboxWriter outboxWriter,
    ILogger<RemoveGroupDmMemberHandler> logger) : IRequestHandler<RemoveGroupDmMemberRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveGroupDmMemberRequest request, CancellationToken cancellationToken)
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

        // Verify it's a group DM
        if (!dmChannel.IsGroup)
        {
            return Error.Validation("NOT_GROUP_DM", "Cannot remove members from 1:1 DM channels");
        }

        // Verify current user is the owner OR is removing themselves
        var isOwner = dmChannel.OwnerId == currentUserId;
        var isRemovingSelf = request.UserId == currentUserId;

        if (!isOwner && !isRemovingSelf)
        {
            return Error.Forbidden("NOT_AUTHORIZED", "Only the owner can remove other members");
        }

        // Find member to remove
        var member = dmChannel.Members.FirstOrDefault(m => m.UserId == request.UserId);
        if (member == null)
        {
            return Error.NotFound("NOT_MEMBER", "User is not a member of this DM channel");
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Remove member
            dbContext.DmChannelMembers.Remove(member);

            // If owner is leaving, transfer ownership or soft-delete if empty
            if (request.UserId == dmChannel.OwnerId)
            {
                var remainingMembers = dmChannel.Members
                    .Where(m => m.UserId != request.UserId)
                    .OrderBy(m => m.JoinedAt)
                    .ToList();

                if (remainingMembers.Count == 0)
                {
                    // No members left, soft-delete the DM channel
                    dmChannel.DeletedAt = DateTimeOffset.UtcNow;
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

            // Write outbox event
            await outboxWriter.WriteAsync(dbContext, "Dm.MemberRemoved", new
            {
                DmChannelId = request.DmChannelId,
                UserId = request.UserId,
                RemovedBy = currentUserId
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "User {UserId} removed from group DM {DmChannelId} by user {CurrentUserId}",
                request.UserId, request.DmChannelId, currentUserId);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/users/@me/dms/{dmChannelId:long}/members/{userId:long}", async (
            long dmChannelId,
            long userId,
            [FromServices] RemoveGroupDmMemberHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new RemoveGroupDmMemberRequest(dmChannelId, userId), ct,
                onSuccess: _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("RemoveGroupDmMember")
        .WithTags("DMs");
}
