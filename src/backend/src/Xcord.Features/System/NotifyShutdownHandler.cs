using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Internal;

/// <summary>
/// Internal endpoint called by xcord-hub before stopping this instance container.
/// Receives a System_ShuttingDown payload and broadcasts it to all connected SignalR
/// clients so they can display a user-friendly suspension notice before the connection
/// drops.  This endpoint is internal-only (accessible within the Docker network on
/// port 80) and does not require user authentication.
/// </summary>
public sealed record NotifyShutdownRequest(string Reason);

public sealed class NotifyShutdownHandler(
    ISystemBroadcaster broadcaster,
    ILogger<NotifyShutdownHandler> logger)
    : IRequestHandler<NotifyShutdownRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(NotifyShutdownRequest request, CancellationToken cancellationToken)
    {
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "suspended by hub" : request.Reason;

        logger.LogInformation(
            "Broadcasting System_ShuttingDown to all connected clients. Reason: {Reason}",
            reason);

        var payload = new
        {
            reason,
            timestamp = DateTimeOffset.UtcNow
        };

        await broadcaster.BroadcastAsync("System_ShuttingDown", payload, cancellationToken);

        logger.LogInformation("System_ShuttingDown broadcast complete");

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/internal/shutdown", async (
            NotifyShutdownRequest request,
            NotifyShutdownHandler handler,
            HttpContext httpContext,
            IConfiguration config,
            CancellationToken ct) =>
        {
            // Internal endpoints require a shared secret header.
            // In production this header is set by xcord-hub when calling instance APIs.
            var internalKey = httpContext.Request.Headers["X-Internal-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(internalKey))
            {
                return Results.Json(new { error = "UNAUTHORIZED", message = "Internal key required" }, statusCode: 401);
            }

            // Validate the key against the configured secret using constant-time comparison
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

            return await handler.ExecuteAsync(request, ct);
        })
        .WithName("NotifyShutdown")
        .WithTags("Internal")
        .AllowAnonymous();
    }
}
