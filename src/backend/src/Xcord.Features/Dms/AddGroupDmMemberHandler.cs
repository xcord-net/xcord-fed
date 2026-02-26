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

namespace Xcord.Features.Dms;

public sealed record AddGroupDmMemberRequest(
    long DmChannelId,
    long UserId
);

public sealed class AddGroupDmMemberHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IOutboxWriter outboxWriter,
    ILogger<AddGroupDmMemberHandler> logger) : IRequestHandler<AddGroupDmMemberRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(AddGroupDmMemberRequest request, CancellationToken cancellationToken)
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
            return Error.Validation("NOT_GROUP_DM", "Cannot add members to 1:1 DM channels");
        }

        // Verify current user is the owner
        if (dmChannel.OwnerId != currentUserId)
        {
            return Error.Forbidden("NOT_OWNER", "Only the group DM owner can add members");
        }

        // Verify user to add exists
        var userExists = await dbContext.Users
            .AnyAsync(u => u.Id == request.UserId, cancellationToken);

        if (!userExists)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        // Check if user is already a member
        var isAlreadyMember = dmChannel.Members.Any(m => m.UserId == request.UserId);
        if (isAlreadyMember)
        {
            return Error.Conflict("ALREADY_MEMBER", "User is already a member of this DM channel");
        }

        // Check max member count (10 members)
        if (dmChannel.Members.Count >= 10)
        {
            return Error.Validation("MAX_MEMBERS", "Group DM channels cannot have more than 10 members");
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Add member
            var member = new DmChannelMember
            {
                UserId = request.UserId,
                DmChannelId = request.DmChannelId,
                JoinedAt = DateTimeOffset.UtcNow
            };

            dbContext.DmChannelMembers.Add(member);

            // Write outbox event
            await outboxWriter.WriteAsync(dbContext, "Dm.MemberAdded", new
            {
                DmChannelId = request.DmChannelId,
                UserId = request.UserId,
                AddedBy = currentUserId
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "User {CurrentUserId} added user {UserId} to group DM {DmChannelId}",
                currentUserId, request.UserId, request.DmChannelId);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/users/@me/dms/{dmChannelId:long}/members/{userId:long}", async (
            long dmChannelId,
            long userId,
            [FromServices] AddGroupDmMemberHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new AddGroupDmMemberRequest(dmChannelId, userId), ct,
                onSuccess: _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("AddGroupDmMember")
        .WithTags("DMs");
}
