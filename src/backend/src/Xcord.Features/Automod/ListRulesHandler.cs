using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Automod;

public sealed record ListRulesQuery(
    long ServerId
);

public sealed record ListRulesResponse(
    List<AutomodRuleDto> Rules
);

public sealed class ListRulesHandler(
    AppDbContext dbContext,
    IRoleService roleService,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListRulesQuery, Result<ListRulesResponse>>
{
    public async Task<Result<ListRulesResponse>> Handle(ListRulesQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify server exists
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Check ManageAutomod permission
        var permissionResult = await roleService.EnsureServerRole(
            userId,
            request.ServerId,
            Role.ManageAutomod);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        var rules = await dbContext.AutomodRules
            .AsNoTracking()
            .Where(r => r.ServerId == request.ServerId)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new AutomodRuleDto(
                r.Id,
                r.ServerId,
                r.Name,
                r.Enabled,
                r.TriggerType,
                r.TriggerConfig,
                r.ActionType,
                r.ActionConfig,
                r.ExemptRoleIds,
                r.ExemptChannelIds,
                r.ExemptBots,
                r.ChannelId,
                r.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new ListRulesResponse(Rules: rules);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/automod-rules", async (
            long serverId,
            IRequestHandler<ListRulesQuery, Result<ListRulesResponse>> handler,
            CancellationToken ct) =>
        {
            var query = new ListRulesQuery(ServerId: serverId);
            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListAutomodRules")
        .WithTags("Automod");
    }
}
