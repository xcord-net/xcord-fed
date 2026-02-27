using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Users;

public sealed record ClearActivityCommand;
public sealed record ClearActivityResponse(bool Cleared);

public sealed class ClearActivityHandler(
    AppDbContext dbContext, ICurrentUserService currentUserService)
    : IRequestHandler<ClearActivityCommand, Result<ClearActivityResponse>>
{
    public async Task<Result<ClearActivityResponse>> Handle(ClearActivityCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var activity = await dbContext.UserActivities.FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (activity != null)
        {
            activity.DeletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
        return new ClearActivityResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/users/@me/activity", async (
            IRequestHandler<ClearActivityCommand, Result<ClearActivityResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ClearActivityCommand(), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ClearActivity").WithTags("Activity");
}
