using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers;

public sealed record UpdateGroupCommand(
    long ServerId,
    long GroupId,
    string? Name,
    string? Color,
    long? Roles,
    int? Position,
    string? LimitsJson
);

public sealed class UpdateGroupHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<UpdateGroupHandler> logger)
    : IRequestHandler<UpdateGroupCommand, Result<GroupDto>>, IValidatable<UpdateGroupCommand>
{
    public Error? Validate(UpdateGroupCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be greater than 0");
        }

        if (request.GroupId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "GroupId must be greater than 0");
        }

        if (request.Name != null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Error.Validation("VALIDATION_ERROR", "Name cannot be empty");
            }

            if (request.Name.Length > 100)
            {
                return Error.Validation("VALIDATION_ERROR", "Name must not exceed 100 characters");
            }
        }

        if (!string.IsNullOrEmpty(request.Color))
        {
            if (request.Color.Length > 7)
            {
                return Error.Validation("VALIDATION_ERROR", "Color must not exceed 7 characters");
            }

            if (!Regex.IsMatch(request.Color, @"^#[0-9A-Fa-f]{6}$"))
            {
                return Error.Validation("VALIDATION_ERROR", "Color must be a valid hex color (e.g., #FF5733)");
            }
        }

        if (request.Roles.HasValue && request.Roles.Value < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Roles must be greater than or equal to 0");
        }

        if (request.Position.HasValue && request.Position.Value < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Position must be greater than or equal to 0");
        }

        return null;
    }

    public async Task<Result<GroupDto>> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
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

        // Role hierarchy check: caller cannot modify groups above their level
        var callerRoles = await roleService.GetServerRoles(userId, request.ServerId).ConfigureAwait(false);
        if (callerRoles != long.MaxValue) // Owner bypasses all checks
        {
            var callerHighestPosition = await roleService.GetHighestGroupPosition(userId, request.ServerId).ConfigureAwait(false);

            // Cannot modify a group at or above caller's highest position
            if (group.Position >= callerHighestPosition)
            {
                return Error.Forbidden("ROLE_HIERARCHY",
                    "You cannot modify a group at or above your highest group position");
            }

            // Cannot grant roles the caller doesn't possess
            if (request.Roles.HasValue)
            {
                var escalatedBits = request.Roles.Value & ~callerRoles;
                if (escalatedBits != 0)
                {
                    return Error.Forbidden("PERMISSION_ESCALATION",
                        "You cannot add roles to a group that you do not have");
                }
            }

            // Cannot move group to a position at or above caller's highest
            if (request.Position.HasValue && request.Position.Value >= callerHighestPosition)
            {
                return Error.Forbidden("ROLE_HIERARCHY",
                    "You cannot move a group to a position at or above your highest group position");
            }
        }

        // Update fields if provided
        if (request.Name != null)
        {
            group.Name = request.Name;
        }

        if (request.Color != null)
        {
            group.Color = request.Color;
        }

        if (request.Roles.HasValue)
        {
            group.Roles = request.Roles.Value;
        }

        if (request.Position.HasValue)
        {
            group.Position = request.Position.Value;
        }

        if (request.LimitsJson != null)
        {
            group.LimitsJson = request.LimitsJson;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} updated group {GroupName} (ID: {GroupId}) in server {ServerId}",
            userId, group.Name, group.Id, request.ServerId);

        // Invalidate server and channel role cache for every member who holds this group -
        // the group's role bitfield (or position) may have changed, affecting their resolved roles.
        await roleService.InvalidateGroupMembersRolesAsync(request.GroupId, request.ServerId, cancellationToken).ConfigureAwait(false);

        return new GroupDto(
            Id: group.Id,
            ServerId: group.ServerId,
            Name: group.Name,
            Color: group.Color,
            Roles: group.Roles,
            Position: group.Position,
            IsEveryone: group.IsEveryone,
            LimitsJson: group.LimitsJson,
            CreatedAt: group.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/servers/{serverId}/groups/{groupId}", async (
            [FromRoute] long serverId,
            [FromRoute] long groupId,
            [FromBody] UpdateGroupRequest request,
            IRequestHandler<UpdateGroupCommand, Result<GroupDto>> handler,
            CancellationToken ct) =>
        {
            var command = new UpdateGroupCommand(
                ServerId: serverId,
                GroupId: groupId,
                Name: request.Name,
                Color: request.Color,
                Roles: request.Roles,
                Position: request.Position,
                LimitsJson: request.LimitsJson
            );

            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Groups")
        .WithName("UpdateGroup")
        .Produces<GroupDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
