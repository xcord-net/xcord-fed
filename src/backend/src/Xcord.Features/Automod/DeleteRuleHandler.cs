using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Automod;

public sealed record DeleteRuleCommand(
    long ServerId,
    long RuleId
);

public sealed class DeleteRuleHandler(
    AppDbContext dbContext,
    IPermissionService permissionService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DeleteRuleHandler> logger)
    : IRequestHandler<DeleteRuleCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteRuleCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Check ManageAutomod permission
        var permissionResult = await permissionService.EnsureServerPermission(
            userId,
            request.ServerId,
            Permission.ManageAutomod);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Get the rule
        var rule = await dbContext.AutomodRules
            .FirstOrDefaultAsync(r => r.Id == request.RuleId && r.ServerId == request.ServerId, cancellationToken);

        if (rule == null)
        {
            return Error.NotFound("RULE_NOT_FOUND", "Automod rule not found");
        }

        // Soft delete
        rule.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} deleted automod rule {RuleName} (ID: {RuleId}) from server {ServerId}",
            userId, rule.Name, rule.Id, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/automod-rules/{ruleId}", async (
            long serverId,
            long ruleId,
            IRequestHandler<DeleteRuleCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new DeleteRuleCommand(ServerId: serverId, RuleId: ruleId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteAutomodRule")
        .WithTags("Automod");
    }
}
