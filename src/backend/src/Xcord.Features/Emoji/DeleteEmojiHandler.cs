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

namespace Xcord.Features.Emoji;

public sealed record DeleteEmojiCommand(
    long ServerId,
    long EmojiId
);

public sealed class DeleteEmojiHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<DeleteEmojiHandler> logger)
    : IRequestHandler<DeleteEmojiCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteEmojiCommand request, CancellationToken cancellationToken)
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

        // Check ManageEmojis permission (or ManageStickers as fallback)
        var permissionResult = await roleService.EnsureServerRole(
            userId,
            request.ServerId,
            Role.ManageEmojis);

        if (permissionResult.IsFailure)
        {
            // Try ManageStickers as alternative
            var altPermissionResult = await roleService.EnsureServerRole(
                userId,
                request.ServerId,
                Role.ManageStickers);

            if (altPermissionResult.IsFailure)
            {
                return Error.Forbidden(
                    "MISSING_PERMISSIONS",
                    "You do not have permission to manage emojis");
            }
        }

        // Find emoji
        var emoji = await dbContext.CustomEmojis
            .FirstOrDefaultAsync(e => e.Id == request.EmojiId && e.ServerId == request.ServerId, cancellationToken);

        if (emoji == null)
        {
            return Error.NotFound("EMOJI_NOT_FOUND", "Emoji not found");
        }

        // Soft delete
        emoji.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} deleted emoji {EmojiName} (ID: {EmojiId}) from server {ServerId}",
            userId, emoji.Name, emoji.Id, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/emojis/{emojiId}", async (
            long serverId,
            long emojiId,
            [FromServices] DeleteEmojiHandler handler,
            CancellationToken ct) =>
        {
            var command = new DeleteEmojiCommand(serverId, emojiId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteEmoji")
        .WithTags("Emoji");
    }
}
