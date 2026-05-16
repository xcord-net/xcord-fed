using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers;

public sealed record AssignGroupCommand(
    long ServerId,
    long TargetUserId,
    long GroupId
);

public sealed class AssignGroupHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<AssignGroupHandler> logger)
    : IRequestHandler<AssignGroupCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(AssignGroupCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check ManageGroups permission
        var permissionCheck = await roleService.EnsureServerRole(
            userId,
            request.ServerId,
            Role.ManageGroups);

        if (permissionCheck.IsFailure)
        {
            return permissionCheck.Error;
        }

        // Verify group exists and belongs to the server
        var group = await dbContext.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == request.GroupId && g.ServerId == request.ServerId, cancellationToken);

        if (group == null)
        {
            return Error.NotFound("GROUP_NOT_FOUND", "Group not found");
        }

        // Role hierarchy check: caller cannot assign groups at or above their level
        var callerRoles = await roleService.GetServerRoles(userId, request.ServerId).ConfigureAwait(false);
        if (callerRoles != long.MaxValue) // Owner bypasses all checks
        {
            var callerHighestPosition = await roleService.GetHighestGroupPosition(userId, request.ServerId).ConfigureAwait(false);

            // Cannot assign a group at or above caller's highest position
            if (group.Position >= callerHighestPosition)
            {
                return Error.Forbidden("ROLE_HIERARCHY",
                    "You cannot assign a group at or above your highest group position");
            }

            // Cannot assign a group with roles the caller doesn't have
            var escalatedBits = group.Roles & ~callerRoles;
            if (escalatedBits != 0)
            {
                return Error.Forbidden("PERMISSION_ESCALATION",
                    "You cannot assign a group with roles you do not have");
            }
        }

        // Verify target user is a member of the server
        var memberExists = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == request.TargetUserId && sm.ServerId == request.ServerId, cancellationToken);

        if (!memberExists)
        {
            return Error.NotFound("MEMBER_NOT_FOUND", "User is not a member of this server");
        }

        // Check if user already has this group
        var existingAssignment = await dbContext.MemberGroups
            .AsNoTracking()
            .AnyAsync(mg => mg.UserId == request.TargetUserId &&
                           mg.ServerId == request.ServerId &&
                           mg.GroupId == request.GroupId,
                      cancellationToken);

        if (existingAssignment)
        {
            return Error.Conflict("GROUP_ALREADY_ASSIGNED", "User already has this group");
        }

        // Create the group assignment
        var memberGroup = new MemberGroup
        {
            UserId = request.TargetUserId,
            ServerId = request.ServerId,
            GroupId = request.GroupId
        };

        dbContext.MemberGroups.Add(memberGroup);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} assigned group {GroupId} to user {TargetUserId} in server {ServerId}",
            userId, request.GroupId, request.TargetUserId, request.ServerId);

        // Invalidate server and channel role cache for the target user - their effective
        // roles have changed now that a new group has been assigned to them.
        await roleService.InvalidateUserRolesAsync(request.TargetUserId, request.ServerId, cancellationToken).ConfigureAwait(false);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/members/{userId}/groups/{groupId}", async (
            [FromRoute] long serverId,
            [FromRoute] long userId,
            [FromRoute] long groupId,
            IRequestHandler<AssignGroupCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new AssignGroupCommand(serverId, userId, groupId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Groups")
        .WithName("AssignGroup")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }
}
