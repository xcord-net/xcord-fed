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
using Xcord.Shared.Extensions;

namespace Xcord.Features.Streambots;

public sealed record DeleteStreambotCommand(long Id);

public sealed class DeleteStreambotHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<DeleteStreambotHandler> logger)
    : IRequestHandler<DeleteStreambotCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        DeleteStreambotCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var streambot = await dbContext.StreamBots
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (streambot == null)
            return Error.NotFound("STREAMBOT_NOT_FOUND", "Stream bot not found");

        // Require ManageBroadcasts on the owning channel
        var permissionResult = await roleService.EnsureChannelRole(
            userId, streambot.ChannelId, Role.ManageBroadcasts);

        if (permissionResult.IsFailure)
            return permissionResult.Error;

        streambot.SoftDelete();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} soft-deleted streambot {StreamBotId}",
            userId, streambot.Id);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/streambots/{id:long}", async (
            long id,
            [FromServices] DeleteStreambotHandler handler,
            CancellationToken ct) =>
        {
            var command = new DeleteStreambotCommand(id);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteStreambot")
        .WithTags("Streambots");
    }
}
