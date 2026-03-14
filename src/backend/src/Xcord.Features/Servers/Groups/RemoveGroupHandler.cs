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

public sealed record RemoveGroupCommand(
    long ServerId,
    long TargetUserId,
    long GroupId
);

public sealed class RemoveGroupHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<RemoveGroupHandler> logger)
    : IRequestHandler<RemoveGroupCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveGroupCommand request, CancellationToken cancellationToken)
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

        // Find the group assignment
        var memberGroup = await dbContext.MemberGroups
            .FirstOrDefaultAsync(mg => mg.UserId == request.TargetUserId &&
                                      mg.ServerId == request.ServerId &&
                                      mg.GroupId == request.GroupId,
                                cancellationToken);

        if (memberGroup == null)
        {
            return Error.NotFound("GROUP_NOT_ASSIGNED", "User does not have this group");
        }

        // Remove the group assignment
        dbContext.MemberGroups.Remove(memberGroup);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} removed group {GroupId} from user {TargetUserId} in server {ServerId}",
            userId, request.GroupId, request.TargetUserId, request.ServerId);

        // Invalidate server and channel role cache for the target user - their effective
        // roles have changed now that a group has been removed from them.
        await roleService.InvalidateUserRolesAsync(request.TargetUserId, request.ServerId, cancellationToken);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/members/{userId}/groups/{groupId}", async (
            [FromRoute] long serverId,
            [FromRoute] long userId,
            [FromRoute] long groupId,
            IRequestHandler<RemoveGroupCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new RemoveGroupCommand(serverId, userId, groupId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Groups")
        .WithName("RemoveGroup")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
