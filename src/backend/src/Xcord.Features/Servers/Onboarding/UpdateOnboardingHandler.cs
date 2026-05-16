using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Servers;

public sealed record UpdateOnboardingCommand(long ServerId, bool IsEnabled, string? DefaultChannelIds, string? RulesText, List<OnboardingPromptDto> Prompts);
public sealed record UpdateOnboardingRequest(bool IsEnabled, string? DefaultChannelIds, string? RulesText, List<OnboardingPromptDto> Prompts);

public sealed class UpdateOnboardingHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService, IRoleService roleService)
    : IRequestHandler<UpdateOnboardingCommand, Result<OnboardingResponse>>
{
    public async Task<Result<OnboardingResponse>> Handle(UpdateOnboardingCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var perm = await roleService.EnsureServerRole(userId, request.ServerId, Role.ManageServer).ConfigureAwait(false);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission");

        var now = DateTimeOffset.UtcNow;
        var config = await dbContext.OnboardingConfigs.Include(o => o.Prompts)
            .FirstOrDefaultAsync(o => o.ServerId == request.ServerId, ct);

        if (config == null)
        {
            config = new OnboardingConfig
            {
                Id = snowflakeGenerator.NextId(), ServerId = request.ServerId,
                IsEnabled = request.IsEnabled, DefaultChannelIds = request.DefaultChannelIds,
                RulesText = request.RulesText, CreatedAt = now, UpdatedAt = now
            };
            dbContext.OnboardingConfigs.Add(config);
        }
        else
        {
            config.IsEnabled = request.IsEnabled;
            config.DefaultChannelIds = request.DefaultChannelIds;
            config.RulesText = request.RulesText;
            config.UpdatedAt = now;
            foreach (var p in config.Prompts.ToList())
                p.DeletedAt = now;
        }

        foreach (var p in request.Prompts)
        {
            if (!Enum.TryParse<OnboardingPromptType>(p.Type, true, out var promptType))
                promptType = OnboardingPromptType.MultipleChoice;
            dbContext.OnboardingPrompts.Add(new OnboardingPrompt
            {
                Id = snowflakeGenerator.NextId(), OnboardingConfigId = config.Id,
                Title = p.Title, Type = promptType, IsRequired = p.IsRequired,
                Position = p.Position, OptionsJson = p.OptionsJson
            });
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return new OnboardingResponse(config.Id, config.ServerId, config.IsEnabled, config.DefaultChannelIds, config.RulesText, request.Prompts);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/servers/{serverId}/onboarding", async (
            long serverId, UpdateOnboardingRequest request,
            IRequestHandler<UpdateOnboardingCommand, Result<OnboardingResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new UpdateOnboardingCommand(serverId, request.IsEnabled, request.DefaultChannelIds, request.RulesText, request.Prompts), ct))
        .RequireAuthorization(Policies.User)
        .WithName("UpdateOnboarding").WithTags("Onboarding");
}
