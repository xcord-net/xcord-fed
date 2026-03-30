using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Config;

public sealed class HubKeyHandler : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users/@me/hub-key", async (
            HttpContext httpContext,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var userId = long.Parse(httpContext.User.FindFirst("sub")!.Value);
            var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
            return Results.Ok(new { hubKey = user.HubKey });
        })
        .RequireAuthorization()
        .WithName("GetHubKey")
        .WithTags("Config");

        return app.MapPut("/api/v1/users/@me/hub-key", async (
            HttpContext httpContext,
            AppDbContext db,
            HubKeyRequest request,
            CancellationToken ct) =>
        {
            var userId = long.Parse(httpContext.User.FindFirst("sub")!.Value);
            var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
            user.HubKey = request.HubKey;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("SetHubKey")
        .WithTags("Config");
    }
}

public sealed record HubKeyRequest(string HubKey);
