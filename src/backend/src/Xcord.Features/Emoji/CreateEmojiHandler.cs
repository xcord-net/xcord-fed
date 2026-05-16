using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Emoji;

public sealed record CreateEmojiCommand(
    long ServerId,
    string Name,
    long AttachmentId,
    bool IsAnimated
);

public sealed record CreateEmojiResponse(
    long Id,
    long ServerId,
    string Name,
    string ImageUrl,
    bool IsAnimated,
    long CreatorId,
    DateTimeOffset CreatedAt
);

public sealed class CreateEmojiHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<CreateEmojiHandler> logger)
    : IRequestHandler<CreateEmojiCommand, Result<CreateEmojiResponse>>
{
    public async Task<Result<CreateEmojiResponse>> Handle(CreateEmojiCommand request, CancellationToken cancellationToken)
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

        // Resolve the attachment to get S3 key and build a stable image URL.
        var attachment = await dbContext.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AttachmentId, cancellationToken);

        if (attachment == null)
        {
            return Error.NotFound("ATTACHMENT_NOT_FOUND", "Attachment not found");
        }

        if (!attachment.IsConfirmed)
        {
            return Error.Validation("ATTACHMENT_NOT_CONFIRMED", "The image has not been uploaded yet");
        }

        // Use the proxy download endpoint so emoji URLs are stable (not time-limited presigned URLs).
        var imageUrl = $"/api/v1/attachments/{attachment.Id}/download";

        // Check emoji name uniqueness within server
        var nameExists = await dbContext.CustomEmojis
            .AsNoTracking()
            .AnyAsync(e => e.ServerId == request.ServerId && e.Name == request.Name, cancellationToken);

        if (nameExists)
        {
            return Error.Conflict("EMOJI_NAME_EXISTS", "An emoji with this name already exists in this server");
        }

        // Check emoji limit (50 static + 50 animated per server)
        var currentEmojiCount = await dbContext.CustomEmojis
            .AsNoTracking()
            .Where(e => e.ServerId == request.ServerId && e.IsAnimated == request.IsAnimated)
            .CountAsync(cancellationToken);

        if (currentEmojiCount >= 50)
        {
            var emojiType = request.IsAnimated ? "animated" : "static";
            return Error.Validation(
                "EMOJI_LIMIT_REACHED",
                $"Server has reached the limit of 50 {emojiType} emojis");
        }

        // Create emoji
        var now = DateTimeOffset.UtcNow;
        var emoji = new CustomEmoji
        {
            Id = snowflakeGenerator.NextId(),
            ServerId = request.ServerId,
            Name = request.Name,
            ImageUrl = imageUrl,
            S3Key = attachment.S3Key,
            IsAnimated = request.IsAnimated,
            CreatorId = userId,
            CreatedAt = now
        };

        dbContext.CustomEmojis.Add(emoji);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} created emoji {EmojiName} (ID: {EmojiId}) in server {ServerId}",
            userId, emoji.Name, emoji.Id, emoji.ServerId);

        return new CreateEmojiResponse(
            Id: emoji.Id,
            ServerId: emoji.ServerId,
            Name: emoji.Name,
            ImageUrl: emoji.ImageUrl,
            IsAnimated: emoji.IsAnimated,
            CreatorId: emoji.CreatorId,
            CreatedAt: emoji.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/emojis", async (
            long serverId,
            CreateEmojiRequest request,
            IRequestHandler<CreateEmojiCommand, Result<CreateEmojiResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateEmojiCommand(
                ServerId: serverId,
                Name: request.Name,
                AttachmentId: request.AttachmentId,
                IsAnimated: request.IsAnimated
            );

            return await handler.ExecuteAsync(command, ct, success => Results.Created($"/api/v1/servers/{serverId}/emojis/{success.Id}", success)).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateEmoji")
        .WithTags("Emoji");
    }
}

public sealed record CreateEmojiRequest(
    string Name,
    long AttachmentId,
    bool IsAnimated
);
