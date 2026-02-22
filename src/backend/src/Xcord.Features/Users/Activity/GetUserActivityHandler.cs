using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Users.Activity;

public sealed record GetUserActivityQuery(long UserId);

public sealed class GetUserActivityHandler(AppDbContext dbContext)
    : IRequestHandler<GetUserActivityQuery, Result<ActivityResponse>>
{
    public async Task<Result<ActivityResponse>> Handle(GetUserActivityQuery request, CancellationToken ct)
    {
        var activity = await dbContext.UserActivities.AsNoTracking()
            .Where(a => a.UserId == request.UserId)
            .Select(a => new ActivityResponse(a.Id, a.UserId, a.ActivityType.ToString(), a.Name,
                a.Details, a.State, a.LargeImageUrl, a.SmallImageUrl, a.StartedAt))
            .FirstOrDefaultAsync(ct);

        if (activity == null) return Error.NotFound("NO_ACTIVITY", "No current activity");
        return activity;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/{userId}/activity", async (
            long userId,
            IRequestHandler<GetUserActivityQuery, Result<ActivityResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetUserActivityQuery(userId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("GetUserActivity").WithTags("Activity");
}
