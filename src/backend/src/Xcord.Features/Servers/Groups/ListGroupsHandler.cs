using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers;

public sealed record ListGroupsQuery(long ServerId);

public sealed class ListGroupsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListGroupsQuery, Result<List<GroupDto>>>
{
    private const int MaxGroupsPerServer = 250;

    public async Task<Result<List<GroupDto>>> Handle(ListGroupsQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check if user is a member of the server
        var memberCheck = await dbContext.EnsureMembership(request.ServerId, userId, cancellationToken).ConfigureAwait(false);
        if (memberCheck.IsFailure) return memberCheck.Error;

        // Get all groups for the server, ordered by position
        var groups = await dbContext.Groups
            .AsNoTracking()
            .Where(g => g.ServerId == request.ServerId)
            .OrderByDescending(g => g.Position)
            .Take(MaxGroupsPerServer)
            .Select(g => new GroupDto(
                g.Id,
                g.ServerId,
                g.Name,
                g.Color,
                g.Roles,
                g.Position,
                g.IsEveryone,
                g.LimitsJson,
                g.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return groups;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/groups", async (
            [FromRoute] long serverId,
            IRequestHandler<ListGroupsQuery, Result<List<GroupDto>>> handler,
            CancellationToken ct) =>
        {
            var query = new ListGroupsQuery(serverId);
            return await handler.ExecuteAsync(query, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithTags("Groups")
        .WithName("ListGroups")
        .Produces<List<GroupDto>>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }
}
