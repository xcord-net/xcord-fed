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

public sealed record CreateRoleCommand(
    long ServerId,
    string Name,
    string? Color,
    long Permissions,
    int Position
);

public sealed class CreateRoleHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    ILogger<CreateRoleHandler> logger)
    : IRequestHandler<CreateRoleCommand, Result<RoleDto>>, IValidatable<CreateRoleCommand>
{
    public Error? Validate(CreateRoleCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("VALIDATION_ERROR", "Name is required");
        }

        if (request.Name.Length > 100)
        {
            return Error.Validation("VALIDATION_ERROR", "Name must not exceed 100 characters");
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

        if (request.Permissions < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Permissions must be greater than or equal to 0");
        }

        if (request.Position < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Position must be greater than or equal to 0");
        }

        return null;
    }

    public async Task<Result<RoleDto>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check if server exists
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
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

        var now = DateTimeOffset.UtcNow;

        // Create role
        var roleId = snowflakeGenerator.NextId();
        var role = new Role
        {
            Id = roleId,
            ServerId = request.ServerId,
            Name = request.Name,
            Color = request.Color,
            Permissions = request.Permissions,
            Position = request.Position,
            IsEveryone = false,
            CreatedAt = now
        };

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created role {RoleName} (ID: {RoleId}) in server {ServerId}",
            userId, role.Name, roleId, request.ServerId);

        // No cache invalidation needed: a freshly created role has no members yet,
        // so no cached permission entries are stale.

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
        return app.MapPost("/api/v1/servers/{serverId}/roles", async (
            [FromRoute] long serverId,
            [FromBody] CreateRoleRequest request,
            IRequestHandler<CreateRoleCommand, Result<RoleDto>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateRoleCommand(
                ServerId: serverId,
                Name: request.Name,
                Color: request.Color,
                Permissions: request.Permissions,
                Position: request.Position
            );

            return await handler.ExecuteAsync(command, ct,
                role => Results.Created($"/api/v1/servers/{serverId}/roles/{role.Id}", role));
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Roles")
        .WithName("CreateRole")
        .Produces<RoleDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
