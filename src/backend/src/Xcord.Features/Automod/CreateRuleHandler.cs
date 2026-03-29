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
    bool ExemptBots,
    long? ChannelId
);

public sealed class CreateRuleHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IRoleService roleService,
    ICurrentUserService currentUserService,
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
            ChannelId = request.ChannelId,
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
            ChannelId: rule.ChannelId,
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
                ExemptBots: request.ExemptBots,
                ChannelId: request.ChannelId
            );

            return await handler.ExecuteAsync(command, ct, success => Results.Created($"/api/v1/servers/{serverId}/automod-rules/{success.Id}", success));
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
    bool ExemptBots,
    long? ChannelId
);
