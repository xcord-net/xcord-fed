using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

public sealed record DeleteChannelCommand(long ChannelId);

public sealed class DeleteChannelHandler(
    AppDbContext dbContext,
    IPermissionService permissionService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DeleteChannelHandler> logger)
    : IRequestHandler<DeleteChannelCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteChannelCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Get channel
        var channel = await dbContext.Channels
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        // Check ManageChannels permission
        var permissionResult = await permissionService.EnsureServerPermission(
            userId,
            channel.ServerId,
            Permission.ManageChannels);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Soft delete
        channel.DeletedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} deleted channel {ChannelId} in server {ServerId}",
            userId, request.ChannelId, channel.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/channels/{channelId}", async (
            long channelId,
            [FromServices] DeleteChannelHandler handler,
            CancellationToken ct) =>
        {
            var command = new DeleteChannelCommand(channelId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteChannel")
        .WithTags("Channels");
    }
}
