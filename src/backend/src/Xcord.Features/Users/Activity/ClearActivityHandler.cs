using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Users.Activity;

public sealed record ClearActivityCommand;
public sealed record ClearActivityResponse(bool Cleared);

public sealed class ClearActivityHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<ClearActivityCommand, Result<ClearActivityResponse>>
{
    public async Task<Result<ClearActivityResponse>> Handle(ClearActivityCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

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
