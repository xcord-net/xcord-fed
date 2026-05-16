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
using Xcord.Shared.Extensions;

namespace Xcord.Features.Servers;

public sealed record DeleteGroupCommand(long ServerId, long GroupId);

public sealed class DeleteGroupHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<DeleteGroupHandler> logger)
    : IRequestHandler<DeleteGroupCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
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

        // Get the group
        var group = await dbContext.Groups
            .FirstOrDefaultAsync(g => g.Id == request.GroupId && g.ServerId == request.ServerId, cancellationToken);

        if (group == null)
        {
            return Error.NotFound("GROUP_NOT_FOUND", "Group not found");
        }

        // Prevent deletion of @everyone group
        if (group.IsEveryone)
        {
            return Error.Validation("CANNOT_DELETE_EVERYONE", "Cannot delete the @everyone group");
        }

        // Collect affected user IDs before soft-deleting so we have them for cache invalidation
        // even if the application later hard-deletes orphaned MemberGroup rows.
        await roleService.InvalidateGroupMembersRolesAsync(request.GroupId, request.ServerId, cancellationToken).ConfigureAwait(false);

        // Soft delete the group
        group.SoftDelete();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} deleted group {GroupName} (ID: {GroupId}) in server {ServerId}",
            userId, group.Name, group.Id, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/groups/{groupId}", async (
            [FromRoute] long serverId,
            [FromRoute] long groupId,
            IRequestHandler<DeleteGroupCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new DeleteGroupCommand(serverId, groupId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Groups")
        .WithName("DeleteGroup")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
