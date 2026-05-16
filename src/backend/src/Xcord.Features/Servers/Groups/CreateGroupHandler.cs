using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Xcord.Entities;
using XcordGroup = Xcord.Entities.Group;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers;

public sealed record CreateGroupCommand(
    long ServerId,
    string Name,
    string? Color,
    long Roles,
    int Position,
    string? LimitsJson
);

public sealed class CreateGroupHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<CreateGroupHandler> logger)
    : IRequestHandler<CreateGroupCommand, Result<GroupDto>>, IValidatable<CreateGroupCommand>
{
    public Error? Validate(CreateGroupCommand request)
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

        if (request.Roles < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Roles must be greater than or equal to 0");
        }

        if (request.Position < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Position must be greater than or equal to 0");
        }

        return null;
    }

    public async Task<Result<GroupDto>> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
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

        // Check ManageGroups permission
        var permissionCheck = await roleService.EnsureServerRole(
            userId,
            request.ServerId,
            Role.ManageGroups);

        if (permissionCheck.IsFailure)
        {
            return permissionCheck.Error;
        }

        // Role hierarchy check: caller cannot create a group with roles they don't have
        var callerRoles = await roleService.GetServerRoles(userId, request.ServerId).ConfigureAwait(false);
        if (callerRoles != long.MaxValue) // Owner bypasses all checks
        {
            // Cannot grant roles the caller doesn't possess
            var escalatedBits = request.Roles & ~callerRoles;
            if (escalatedBits != 0)
            {
                return Error.Forbidden("PERMISSION_ESCALATION",
                    "You cannot create a group with roles you do not have");
            }

            // Cannot create a group at or above caller's highest group position
            var callerHighestPosition = await roleService.GetHighestGroupPosition(userId, request.ServerId).ConfigureAwait(false);
            if (request.Position >= callerHighestPosition)
            {
                return Error.Forbidden("ROLE_HIERARCHY",
                    "You cannot create a group at or above your highest group position");
            }
        }

        var now = DateTimeOffset.UtcNow;

        // Create group
        var groupId = snowflakeGenerator.NextId();
        var group = new XcordGroup
        {
            Id = groupId,
            ServerId = request.ServerId,
            Name = request.Name,
            Color = request.Color,
            Roles = request.Roles,
            Position = request.Position,
            IsEveryone = false,
            LimitsJson = request.LimitsJson,
            CreatedAt = now
        };

        dbContext.Groups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} created group {GroupName} (ID: {GroupId}) in server {ServerId}",
            userId, group.Name, groupId, request.ServerId);

        // No cache invalidation needed: a freshly created group has no members yet,
        // so no cached role entries are stale.

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
        return app.MapPost("/api/v1/servers/{serverId}/groups", async (
            [FromRoute] long serverId,
            [FromBody] CreateGroupRequest request,
            IRequestHandler<CreateGroupCommand, Result<GroupDto>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateGroupCommand(
                ServerId: serverId,
                Name: request.Name,
                Color: request.Color,
                Roles: request.Roles,
                Position: request.Position,
                LimitsJson: request.LimitsJson
            );

            return await handler.ExecuteAsync(command, ct,
                group => Results.Created($"/api/v1/servers/{serverId}/groups/{group.Id}", group));
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Groups")
        .WithName("CreateGroup")
        .Produces<GroupDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
