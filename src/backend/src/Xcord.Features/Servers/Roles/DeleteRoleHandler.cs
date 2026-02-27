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

public sealed record DeleteRoleCommand(long ServerId, long RoleId);

public sealed class DeleteRoleHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    ILogger<DeleteRoleHandler> logger)
    : IRequestHandler<DeleteRoleCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
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

        // Get the role
        var role = await dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId && r.ServerId == request.ServerId, cancellationToken);

        if (role == null)
        {
            return Error.NotFound("ROLE_NOT_FOUND", "Role not found");
        }

        // Prevent deletion of @everyone role
        if (role.IsEveryone)
        {
            return Error.Validation("CANNOT_DELETE_EVERYONE", "Cannot delete the @everyone role");
        }

        // Collect affected user IDs before soft-deleting so we have them for cache invalidation
        // even if the application later hard-deletes orphaned MemberRole rows.
        await permissionService.InvalidateRoleMembersPermissionsAsync(request.RoleId, request.ServerId, cancellationToken);

        // Soft delete the role
        role.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} deleted role {RoleName} (ID: {RoleId}) in server {ServerId}",
            userId, role.Name, role.Id, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/roles/{roleId}", async (
            [FromRoute] long serverId,
            [FromRoute] long roleId,
            IRequestHandler<DeleteRoleCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new DeleteRoleCommand(serverId, roleId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Roles")
        .WithName("DeleteRole")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
