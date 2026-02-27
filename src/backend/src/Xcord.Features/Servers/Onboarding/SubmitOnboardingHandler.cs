using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Servers;

public sealed record SubmitOnboardingCommand(long ServerId, string ResponseDataJson);
public sealed record SubmitOnboardingRequest(string ResponseDataJson);
public sealed record SubmitOnboardingResult(bool Completed);

public sealed class SubmitOnboardingHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService)
    : IRequestHandler<SubmitOnboardingCommand, Result<SubmitOnboardingResult>>
{
    public async Task<Result<SubmitOnboardingResult>> Handle(SubmitOnboardingCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var existing = await dbContext.OnboardingCompletions.AsNoTracking()
            .AnyAsync(r => r.ServerId == request.ServerId && r.UserId == userId, ct);
        if (existing) return Error.Conflict("ALREADY_COMPLETED", "Onboarding already completed");

        var completion = new OnboardingCompletion
        {
            Id = snowflakeGenerator.NextId(), ServerId = request.ServerId,
            UserId = userId, CompletedAt = DateTimeOffset.UtcNow, ResponseDataJson = request.ResponseDataJson
        };
        dbContext.OnboardingCompletions.Add(completion);
        await dbContext.SaveChangesAsync(ct);
        return new SubmitOnboardingResult(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/servers/{serverId}/onboarding/complete", async (
            long serverId, SubmitOnboardingRequest request,
            IRequestHandler<SubmitOnboardingCommand, Result<SubmitOnboardingResult>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new SubmitOnboardingCommand(serverId, request.ResponseDataJson), ct))
        .RequireAuthorization(Policies.User)
        .WithName("SubmitOnboarding").WithTags("Onboarding");
}
