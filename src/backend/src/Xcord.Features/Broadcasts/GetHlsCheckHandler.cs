using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Broadcasts;

/// <summary>
/// Fast-path authorization probe used by Caddy's <c>forward_auth</c> directive before
/// serving HLS segments from MinIO. Returns 200 for authorized viewers, 401/403 otherwise.
/// No DB writes, no side effects - this endpoint is called for every HLS segment fetch.
/// </summary>
public sealed class GetHlsCheckHandler : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/broadcasts/{broadcastId:long}/hls-check", async (
            long broadcastId,
            [FromServices] AppDbContext dbContext,
            [FromServices] ICurrentUserService currentUserService,
            [FromServices] IRoleService roleService,
            CancellationToken ct) =>
        {
            var userIdResult = currentUserService.GetCurrentUserId();
            if (userIdResult.IsFailure)
                return Results.Unauthorized();
            var userId = userIdResult.Value;

            var channelId = await dbContext.Broadcasts
                .AsNoTracking()
                .Where(b => b.Id == broadcastId)
                .Select(b => (long?)b.ChannelId)
                .FirstOrDefaultAsync(ct);

            if (channelId == null)
                return Results.NotFound();

            var permission = await roleService.EnsureChannelRole(
                userId, channelId.Value, Role.ViewBroadcast);
            if (permission.IsFailure)
                return Results.StatusCode(permission.Error.StatusCode);

            return Results.Ok();
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("BroadcastHlsCheck")
        .WithTags("Broadcasts");
    }
}
