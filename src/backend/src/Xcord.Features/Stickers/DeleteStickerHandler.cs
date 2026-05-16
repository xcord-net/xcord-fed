using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Stickers;

public sealed record DeleteStickerCommand(
    long ServerId,
    long StickerId
);

public sealed class DeleteStickerHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<DeleteStickerHandler> logger)
    : IRequestHandler<DeleteStickerCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteStickerCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify server exists
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Check ManageStickers permission (or ManageEmojis as fallback)
        var permissionResult = await roleService.EnsureServerRole(
            userId,
            request.ServerId,
            Role.ManageStickers);

        if (permissionResult.IsFailure)
        {
            // Try ManageEmojis as alternative
            var altPermissionResult = await roleService.EnsureServerRole(
                userId,
                request.ServerId,
                Role.ManageEmojis);

            if (altPermissionResult.IsFailure)
            {
                return Error.Forbidden(
                    "MISSING_PERMISSIONS",
                    "You do not have permission to manage stickers");
            }
        }

        // Find sticker and verify it belongs to the server
        var sticker = await dbContext.Stickers
            .Include(s => s.StickerPack)
            .FirstOrDefaultAsync(s => s.Id == request.StickerId, cancellationToken);

        if (sticker == null)
        {
            return Error.NotFound("STICKER_NOT_FOUND", "Sticker not found");
        }

        if (sticker.StickerPack.ServerId != request.ServerId)
        {
            return Error.Forbidden("STICKER_NOT_IN_SERVER", "Sticker does not belong to this server");
        }

        // Soft delete
        sticker.SoftDelete();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} deleted sticker {StickerName} (ID: {StickerId}) from server {ServerId}",
            userId, sticker.Name, sticker.Id, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/stickers/{stickerId}", async (
            long serverId,
            long stickerId,
            [FromServices] DeleteStickerHandler handler,
            CancellationToken ct) =>
        {
            var command = new DeleteStickerCommand(serverId, stickerId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteSticker")
        .WithTags("Stickers");
    }
}
