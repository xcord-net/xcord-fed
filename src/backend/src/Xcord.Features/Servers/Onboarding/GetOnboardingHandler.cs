using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Servers.Onboarding;

public sealed record GetOnboardingQuery(long ServerId);
public sealed record OnboardingPromptDto(long Id, string Title, string Type, bool IsRequired, int Position, string OptionsJson);
public sealed record OnboardingResponse(long Id, long ServerId, bool IsEnabled, string? DefaultChannelIds, string? RulesText, List<OnboardingPromptDto> Prompts);

public sealed class GetOnboardingHandler(AppDbContext dbContext)
    : IRequestHandler<GetOnboardingQuery, Result<OnboardingResponse>>
{
    public async Task<Result<OnboardingResponse>> Handle(GetOnboardingQuery request, CancellationToken ct)
    {
        var config = await dbContext.OnboardingConfigs.AsNoTracking()
            .Include(o => o.Prompts.OrderBy(p => p.Position))
            .FirstOrDefaultAsync(o => o.ServerId == request.ServerId, ct);

        if (config == null)
            return new OnboardingResponse(0, request.ServerId, false, null, null, new List<OnboardingPromptDto>());

        var prompts = config.Prompts.Select(p => new OnboardingPromptDto(p.Id, p.Title, p.Type.ToString(), p.IsRequired, p.Position, p.OptionsJson)).ToList();
        return new OnboardingResponse(config.Id, config.ServerId, config.IsEnabled, config.DefaultChannelIds, config.RulesText, prompts);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/servers/{serverId}/onboarding", async (
            long serverId,
            IRequestHandler<GetOnboardingQuery, Result<OnboardingResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetOnboardingQuery(serverId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("GetOnboarding").WithTags("Onboarding");
}
