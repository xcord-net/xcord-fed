using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Internal;

public sealed class GetInstanceStatsHandler : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/internal/stats", async (
            AppDbContext db,
            IStorageService storage,
            CancellationToken ct) =>
        {
            var activeThreshold = DateTimeOffset.UtcNow.AddDays(-30);

            var totalUsers = await db.Users.CountAsync(u =>
                u.DeletedAt == null && !u.IsBot && !u.IsDisabled, ct);

            var activeUsers = await db.Users.CountAsync(u =>
                u.DeletedAt == null && !u.IsBot && !u.IsDisabled &&
                u.LastLoginAt >= activeThreshold, ct);

            var serverCount = await db.Servers.CountAsync(s =>
                s.DeletedAt == null, ct);

            var storageUsedBytes = await storage.GetBucketSizeAsync(ct).ConfigureAwait(false);

            return Results.Json(new
            {
                totalUsers,
                activeUsers,
                serverCount,
                storageUsedBytes
            });
        })
        .WithName("GetInstanceStats")
        .WithTags("Internal")
        .RequireAuthorization(Policies.InternalKey);
    }
}
