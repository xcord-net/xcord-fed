using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers.Roles;

public sealed record UpdateRoleCommand(
    long ServerId,
    long RoleId,
    string? Name,
    string? Color,
    long? Permissions,
    int? Position
);

public sealed class UpdateRoleHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService,
    ILogger<UpdateRoleHandler> logger)
    : IRequestHandler<UpdateRoleCommand, Result<RoleDto>>, IValidatable<UpdateRoleCommand>
{
    public Error? Validate(UpdateRoleCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be greater than 0");
        }

        if (request.RoleId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "RoleId must be greater than 0");
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

        if (request.Permissions.HasValue && request.Permissions.Value < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Permissions must be greater than or equal to 0");
        }

        if (request.Position.HasValue && request.Position.Value < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Position must be greater than or equal to 0");
        }

        return null;
    }

    public async Task<Result<RoleDto>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
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

        // Get the role
        var role = await dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId && r.ServerId == request.ServerId, cancellationToken);

        if (role == null)
        {
            return Error.NotFound("ROLE_NOT_FOUND", "Role not found");
        }

        // Update fields if provided
        if (request.Name != null)
        {
            role.Name = request.Name;
        }

        if (request.Color != null)
        {
            role.Color = request.Color;
        }

        if (request.Permissions.HasValue)
        {
            role.Permissions = request.Permissions.Value;
        }

        if (request.Position.HasValue)
        {
            role.Position = request.Position.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} updated role {RoleName} (ID: {RoleId}) in server {ServerId}",
            userId, role.Name, role.Id, request.ServerId);

        // Invalidate server and channel permission cache for every member who holds this role —
        // the role's permission bitfield (or position) may have changed, affecting their resolved permissions.
        await permissionService.InvalidateRoleMembersPermissionsAsync(request.RoleId, request.ServerId, cancellationToken);

        return new RoleDto(
            Id: role.Id,
            ServerId: role.ServerId,
            Name: role.Name,
            Color: role.Color,
            Permissions: role.Permissions,
            Position: role.Position,
            IsEveryone: role.IsEveryone,
            CreatedAt: role.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/servers/{serverId}/roles/{roleId}", async (
            [FromRoute] long serverId,
            [FromRoute] long roleId,
            [FromBody] UpdateRoleRequest request,
            IRequestHandler<UpdateRoleCommand, Result<RoleDto>> handler,
            CancellationToken ct) =>
        {
            var command = new UpdateRoleCommand(
                ServerId: serverId,
                RoleId: roleId,
                Name: request.Name,
                Color: request.Color,
                Permissions: request.Permissions,
                Position: request.Position
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Roles")
        .WithName("UpdateRole")
        .Produces<RoleDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
