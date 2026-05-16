using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Automod;

public sealed record UpdateRuleCommand(
    long ServerId,
    long RuleId,
    string? Name,
    bool? Enabled,
    AutomodTriggerType? TriggerType,
    string? TriggerConfig,
    AutomodActionType? ActionType,
    string? ActionConfig,
    string? ExemptRoleIds,
    string? ExemptChannelIds,
    bool? ExemptBots,
    long? ChannelId,
    bool ClearChannelId = false
);

public sealed class UpdateRuleHandler(
    AppDbContext dbContext,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    ILogger<UpdateRuleHandler> logger)
    : IRequestHandler<UpdateRuleCommand, Result<AutomodRuleDto>>
{
    public async Task<Result<AutomodRuleDto>> Handle(UpdateRuleCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check ManageAutomod permission
        var permissionResult = await roleService.EnsureServerRole(
            userId,
            request.ServerId,
            Role.ManageAutomod);

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

        // Update fields if provided
        if (request.Name != null)
        {
            rule.Name = request.Name;
        }

        if (request.Enabled.HasValue)
        {
            rule.Enabled = request.Enabled.Value;
        }

        if (request.TriggerType.HasValue)
        {
            rule.TriggerType = request.TriggerType.Value;
        }

        if (request.TriggerConfig != null)
        {
            rule.TriggerConfig = request.TriggerConfig;
        }

        if (request.ActionType.HasValue)
        {
            rule.ActionType = request.ActionType.Value;
        }

        if (request.ActionConfig != null)
        {
            rule.ActionConfig = request.ActionConfig;
        }

        if (request.ExemptRoleIds != null)
        {
            rule.ExemptRoleIds = request.ExemptRoleIds;
        }

        if (request.ExemptChannelIds != null)
        {
            rule.ExemptChannelIds = request.ExemptChannelIds;
        }

        if (request.ExemptBots.HasValue)
        {
            rule.ExemptBots = request.ExemptBots.Value;
        }

        if (request.ClearChannelId)
        {
            rule.ChannelId = null;
        }
        else if (request.ChannelId.HasValue)
        {
            rule.ChannelId = request.ChannelId.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} updated automod rule {RuleName} (ID: {RuleId}) for server {ServerId}",
            userId, rule.Name, rule.Id, request.ServerId);

        return new AutomodRuleDto(
            Id: rule.Id,
            ServerId: rule.ServerId,
            Name: rule.Name,
            Enabled: rule.Enabled,
            TriggerType: rule.TriggerType,
            TriggerConfig: rule.TriggerConfig,
            ActionType: rule.ActionType,
            ActionConfig: rule.ActionConfig,
            ExemptRoleIds: rule.ExemptRoleIds,
            ExemptChannelIds: rule.ExemptChannelIds,
            ExemptBots: rule.ExemptBots,
            ChannelId: rule.ChannelId,
            CreatedAt: rule.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/servers/{serverId}/automod-rules/{ruleId}", async (
            long serverId,
            long ruleId,
            UpdateRuleRequest request,
            IRequestHandler<UpdateRuleCommand, Result<AutomodRuleDto>> handler,
            CancellationToken ct) =>
        {
            var command = new UpdateRuleCommand(
                ServerId: serverId,
                RuleId: ruleId,
                Name: request.Name,
                Enabled: request.Enabled,
                TriggerType: request.TriggerType,
                TriggerConfig: request.TriggerConfig,
                ActionType: request.ActionType,
                ActionConfig: request.ActionConfig,
                ExemptRoleIds: request.ExemptRoleIds,
                ExemptChannelIds: request.ExemptChannelIds,
                ExemptBots: request.ExemptBots,
                ChannelId: request.ChannelId,
                ClearChannelId: request.ClearChannelId
            );

            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateAutomodRule")
        .WithTags("Automod");
    }
}

internal sealed record UpdateRuleRequest(
    string? Name,
    bool? Enabled,
    Entities.AutomodTriggerType? TriggerType,
    string? TriggerConfig,
    Entities.AutomodActionType? ActionType,
    string? ActionConfig,
    string? ExemptRoleIds,
    string? ExemptChannelIds,
    bool? ExemptBots,
    long? ChannelId,
    bool ClearChannelId = false
);
