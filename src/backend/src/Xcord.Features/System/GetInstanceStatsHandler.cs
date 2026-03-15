using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
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
            HttpContext httpContext,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var internalKey = httpContext.Request.Headers["X-Internal-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(internalKey))
            {
                return Results.Json(new { error = "UNAUTHORIZED", message = "Internal key required" }, statusCode: 401);
            }

            var configuredKey = config["InternalApi:Key"];
            if (string.IsNullOrEmpty(configuredKey))
            {
                return Results.Json(new { error = "UNAUTHORIZED", message = "Internal API key not configured" }, statusCode: 401);
            }

            var providedBytes = Encoding.UTF8.GetBytes(internalKey);
            var expectedBytes = Encoding.UTF8.GetBytes(configuredKey);
            if (!CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            {
                return Results.Json(new { error = "UNAUTHORIZED", message = "Invalid internal key" }, statusCode: 401);
            }

            var activeThreshold = DateTimeOffset.UtcNow.AddDays(-30);

            var totalUsers = await db.Users.CountAsync(u =>
                u.DeletedAt == null && !u.IsBot && !u.IsDisabled, ct);

            var activeUsers = await db.Users.CountAsync(u =>
                u.DeletedAt == null && !u.IsBot && !u.IsDisabled &&
                u.LastLoginAt >= activeThreshold, ct);

            var serverCount = await db.Servers.CountAsync(s =>
                s.DeletedAt == null, ct);

            var storageUsedBytes = await storage.GetBucketSizeAsync(ct);

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
        .AllowAnonymous();
    }
}
