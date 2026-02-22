using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers.Roles;

public sealed record RemoveRoleCommand(
    long ServerId,
    long TargetUserId,
    long RoleId
);

public sealed class RemoveRoleHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService,
    ILogger<RemoveRoleHandler> logger)
    : IRequestHandler<RemoveRoleCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveRoleCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Check ManageRoles permission
        var permissionCheck = await permissionService.EnsureServerPermission(
            userId,
            request.ServerId,
            Permission.ManageRoles);

        if (permissionCheck.IsFailure)
        {
            return permissionCheck.Error;
        }

        // Find the role assignment
        var memberRole = await dbContext.MemberRoles
            .FirstOrDefaultAsync(mr => mr.UserId == request.TargetUserId &&
                                      mr.ServerId == request.ServerId &&
                                      mr.RoleId == request.RoleId,
                                cancellationToken);

        if (memberRole == null)
        {
            return Error.NotFound("ROLE_NOT_ASSIGNED", "User does not have this role");
        }

        // Remove the role assignment
        dbContext.MemberRoles.Remove(memberRole);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} removed role {RoleId} from user {TargetUserId} in server {ServerId}",
            userId, request.RoleId, request.TargetUserId, request.ServerId);

        // Invalidate server and channel permission cache for the target user — their effective
        // permissions have changed now that a role has been removed from them.
        await permissionService.InvalidateUserPermissionsAsync(request.TargetUserId, request.ServerId, cancellationToken);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/members/{userId}/roles/{roleId}", async (
            [FromRoute] long serverId,
            [FromRoute] long userId,
            [FromRoute] long roleId,
            IRequestHandler<RemoveRoleCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new RemoveRoleCommand(serverId, userId, roleId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Roles")
        .WithName("RemoveRole")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
