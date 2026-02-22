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

public sealed record CreateRuleCommand(
    long ServerId,
    string Name,
    bool Enabled,
    AutomodTriggerType TriggerType,
    string TriggerConfig,
    AutomodActionType ActionType,
    string? ActionConfig,
    string? ExemptRoleIds,
    string? ExemptChannelIds,
    bool ExemptBots
);

public sealed class CreateRuleHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IPermissionService permissionService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CreateRuleHandler> logger)
    : IRequestHandler<CreateRuleCommand, Result<AutomodRuleDto>>, IValidatable<CreateRuleCommand>
{
    public Error? Validate(CreateRuleCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be a valid snowflake ID");
        }

        if (string.IsNullOrEmpty(request.Name) || request.Name.Length > 100)
        {
            return Error.Validation("VALIDATION_ERROR", "Name must be between 1 and 100 characters");
        }

        if (string.IsNullOrEmpty(request.TriggerConfig))
        {
            return Error.Validation("VALIDATION_ERROR", "TriggerConfig is required");
        }

        if (!Enum.IsDefined(typeof(AutomodTriggerType), request.TriggerType))
        {
            return Error.Validation("VALIDATION_ERROR", "Invalid TriggerType");
        }

        if (!Enum.IsDefined(typeof(AutomodActionType), request.ActionType))
        {
            return Error.Validation("VALIDATION_ERROR", "Invalid ActionType");
        }

        return null;
    }

    public async Task<Result<AutomodRuleDto>> Handle(CreateRuleCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Verify server exists
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
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

        var now = DateTimeOffset.UtcNow;

        var rule = new AutomodRule
        {
            Id = snowflakeGenerator.NextId(),
            ServerId = request.ServerId,
            Name = request.Name,
            Enabled = request.Enabled,
            TriggerType = request.TriggerType,
            TriggerConfig = request.TriggerConfig,
            ActionType = request.ActionType,
            ActionConfig = request.ActionConfig,
            ExemptRoleIds = request.ExemptRoleIds,
            ExemptChannelIds = request.ExemptChannelIds,
            ExemptBots = request.ExemptBots,
            CreatedAt = now
        };

        dbContext.AutomodRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created automod rule {RuleName} (ID: {RuleId}) for server {ServerId}",
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
            CreatedAt: rule.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/automod-rules", async (
            long serverId,
            CreateRuleRequest request,
            IRequestHandler<CreateRuleCommand, Result<AutomodRuleDto>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateRuleCommand(
                ServerId: serverId,
                Name: request.Name,
                Enabled: request.Enabled,
                TriggerType: request.TriggerType,
                TriggerConfig: request.TriggerConfig,
                ActionType: request.ActionType,
                ActionConfig: request.ActionConfig,
                ExemptRoleIds: request.ExemptRoleIds,
                ExemptChannelIds: request.ExemptChannelIds,
                ExemptBots: request.ExemptBots
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateAutomodRule")
        .WithTags("Automod");
    }
}

internal sealed record CreateRuleRequest(
    string Name,
    bool Enabled,
    Entities.AutomodTriggerType TriggerType,
    string TriggerConfig,
    Entities.AutomodActionType ActionType,
    string? ActionConfig,
    string? ExemptRoleIds,
    string? ExemptChannelIds,
    bool ExemptBots
);
