using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Streambots;

public sealed record TestStreambotCommand(long Id);

public sealed record TestStreambotResponse(bool Success, string? Error);

/// <summary>
/// Performs a lightweight connectivity probe against a streambot's RTMP ingest endpoint.
///
/// MVP behaviour: parse the RTMP URL, resolve DNS, attempt a short TCP connect to the
/// ingest port (defaulting to 1935, or the explicit port if provided). A full test egress
/// flow that actually publishes a validation stream is Phase 2 work.
/// </summary>
public sealed class TestStreambotHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<TestStreambotHandler> logger)
    : IRequestHandler<TestStreambotCommand, Result<TestStreambotResponse>>
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private const int DefaultRtmpPort = 1935;

    public async Task<Result<TestStreambotResponse>> Handle(
        TestStreambotCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var streambot = await dbContext.StreamBots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (streambot == null)
            return Error.NotFound("STREAMBOT_NOT_FOUND", "Stream bot not found");

        var permissionResult = await roleService.EnsureChannelRole(
            userId, streambot.ChannelId, Role.ManageBroadcasts);

        if (permissionResult.IsFailure)
            return permissionResult.Error;

        if (!Uri.TryCreate(streambot.RtmpUrl, UriKind.Absolute, out var uri))
        {
            return new TestStreambotResponse(Success: false, Error: "Invalid RTMP URL");
        }

        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            return new TestStreambotResponse(Success: false, Error: "RTMP URL is missing a host");
        }

        var port = uri.IsDefaultPort ? DefaultRtmpPort : uri.Port;

        // MVP: resolve DNS + TCP-connect only. Phase 2 will perform a full RTMP handshake
        // and short-duration publish to validate credentials end-to-end.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ConnectTimeout);

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, timeoutCts.Token);

            logger.LogInformation(
                "User {UserId} tested streambot {StreamBotId}: TCP connect to {Host}:{Port} succeeded",
                userId, streambot.Id, host, port);

            return new TestStreambotResponse(Success: true, Error: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Streambot {StreamBotId} test timed out connecting to {Host}:{Port}",
                streambot.Id, host, port);
            return new TestStreambotResponse(
                Success: false,
                Error: $"Connection to {host}:{port} timed out after {ConnectTimeout.TotalSeconds:0} seconds");
        }
        catch (SocketException ex)
        {
            logger.LogWarning(ex,
                "Streambot {StreamBotId} test failed: socket error {Host}:{Port}",
                streambot.Id, host, port);
            return new TestStreambotResponse(
                Success: false,
                Error: $"Could not connect to {host}:{port}: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Streambot {StreamBotId} test failed with unexpected error",
                streambot.Id);
            return new TestStreambotResponse(Success: false, Error: ex.Message);
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/streambots/{id:long}/test", async (
            long id,
            [FromServices] TestStreambotHandler handler,
            CancellationToken ct) =>
        {
            var command = new TestStreambotCommand(id);
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("TestStreambot")
        .WithTags("Streambots");
    }
}
