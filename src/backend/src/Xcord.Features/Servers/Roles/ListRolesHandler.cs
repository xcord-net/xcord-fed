using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers.Roles;

public sealed record ListRolesQuery(long ServerId);

public sealed class ListRolesHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListRolesQuery, Result<List<RoleDto>>>
{
    private const int MaxRolesPerServer = 250;

    public async Task<Result<List<RoleDto>>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check if user is a member of the server
        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == request.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_A_MEMBER", "You are not a member of this server");
        }

        // Get all roles for the server, ordered by position
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Where(r => r.ServerId == request.ServerId)
            .OrderByDescending(r => r.Position)
            .Take(MaxRolesPerServer)
            .Select(r => new RoleDto(
                r.Id,
                r.ServerId,
                r.Name,
                r.Color,
                r.Permissions,
                r.Position,
                r.IsEveryone,
                r.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return roles;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/roles", async (
            [FromRoute] long serverId,
            IRequestHandler<ListRolesQuery, Result<List<RoleDto>>> handler,
            CancellationToken ct) =>
        {
            var query = new ListRolesQuery(serverId);
            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Roles")
        .WithName("ListRoles")
        .Produces<List<RoleDto>>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }
}
