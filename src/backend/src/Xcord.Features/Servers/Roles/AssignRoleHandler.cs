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

public sealed record AssignRoleCommand(
    long ServerId,
    long TargetUserId,
    long RoleId
);

public sealed class AssignRoleHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    ILogger<AssignRoleHandler> logger)
    : IRequestHandler<AssignRoleCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check ManageRoles permission
        var permissionCheck = await permissionService.EnsureServerPermission(
            userId,
            request.ServerId,
            Permission.ManageRoles);

        if (permissionCheck.IsFailure)
        {
            return permissionCheck.Error;
        }

        // Verify role exists and belongs to the server
        var roleExists = await dbContext.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == request.RoleId && r.ServerId == request.ServerId, cancellationToken);

        if (!roleExists)
        {
            return Error.NotFound("ROLE_NOT_FOUND", "Role not found");
        }

        // Verify target user is a member of the server
        var memberExists = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == request.TargetUserId && sm.ServerId == request.ServerId, cancellationToken);

        if (!memberExists)
        {
            return Error.NotFound("MEMBER_NOT_FOUND", "User is not a member of this server");
        }

        // Check if user already has this role
        var existingAssignment = await dbContext.MemberRoles
            .AsNoTracking()
            .AnyAsync(mr => mr.UserId == request.TargetUserId &&
                           mr.ServerId == request.ServerId &&
                           mr.RoleId == request.RoleId,
                      cancellationToken);

        if (existingAssignment)
        {
            return Error.Conflict("ROLE_ALREADY_ASSIGNED", "User already has this role");
        }

        // Create the role assignment
        var memberRole = new MemberRole
        {
            UserId = request.TargetUserId,
            ServerId = request.ServerId,
            RoleId = request.RoleId
        };

        dbContext.MemberRoles.Add(memberRole);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} assigned role {RoleId} to user {TargetUserId} in server {ServerId}",
            userId, request.RoleId, request.TargetUserId, request.ServerId);

        // Invalidate server and channel permission cache for the target user — their effective
        // permissions have changed now that a new role has been assigned to them.
        await permissionService.InvalidateUserPermissionsAsync(request.TargetUserId, request.ServerId, cancellationToken);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/members/{userId}/roles/{roleId}", async (
            [FromRoute] long serverId,
            [FromRoute] long userId,
            [FromRoute] long roleId,
            IRequestHandler<AssignRoleCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new AssignRoleCommand(serverId, userId, roleId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Roles")
        .WithName("AssignRole")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }
}
