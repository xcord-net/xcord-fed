using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
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
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(request, ct);
        })
        .WithName("NotifyShutdown")
        .WithTags("Internal")
        .AllowAnonymous();
    }
}
