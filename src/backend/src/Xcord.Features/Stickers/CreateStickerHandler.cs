using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Stickers;

public sealed record CreateStickerCommand(
    long ServerId,
    long PackId,
    string Name,
    string? Tags,
    long AttachmentId
);

public sealed record CreateStickerResponse(
    long Id,
    long StickerPackId,
    string Name,
    string? Tags,
    string ImageUrl,
    DateTimeOffset CreatedAt
);

public sealed class CreateStickerHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    ILogger<CreateStickerHandler> logger)
    : IRequestHandler<CreateStickerCommand, Result<CreateStickerResponse>>, IValidatable<CreateStickerCommand>
{
    public Error? Validate(CreateStickerCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be a valid snowflake");
        }

        if (request.PackId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "PackId must be a valid snowflake");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("VALIDATION_ERROR", "Name is required");
        }

        if (request.Name.Length > 32)
        {
            return Error.Validation("VALIDATION_ERROR", "Name must not exceed 32 characters");
        }

        if (request.Tags != null && request.Tags.Length > 200)
        {
            return Error.Validation("VALIDATION_ERROR", "Tags must not exceed 200 characters");
        }

        if (request.AttachmentId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "AttachmentId must be a valid snowflake");
        }

        return null;
    }

    public async Task<Result<CreateStickerResponse>> Handle(CreateStickerCommand request, CancellationToken cancellationToken)
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
        var permissionResult = await permissionService.EnsureServerPermission(
            userId,
            request.ServerId,
            Permission.ManageStickers);

        if (permissionResult.IsFailure)
        {
            // Try ManageEmojis as alternative
            var altPermissionResult = await permissionService.EnsureServerPermission(
                userId,
                request.ServerId,
                Permission.ManageEmojis);

            if (altPermissionResult.IsFailure)
            {
                return Error.Forbidden(
                    "MISSING_PERMISSIONS",
                    "You do not have permission to manage stickers");
            }
        }

        // Verify sticker pack exists and belongs to the server
        var packExists = await dbContext.StickerPacks
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.PackId && p.ServerId == request.ServerId, cancellationToken);

        if (!packExists)
        {
            return Error.NotFound("STICKER_PACK_NOT_FOUND", "Sticker pack not found in this server");
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

        // Use the proxy download endpoint so sticker URLs are stable (not time-limited presigned URLs).
        var imageUrl = $"/api/v1/attachments/{attachment.Id}/download";

        // Create sticker
        var now = DateTimeOffset.UtcNow;
        var sticker = new Sticker
        {
            Id = snowflakeGenerator.NextId(),
            StickerPackId = request.PackId,
            Name = request.Name,
            Tags = request.Tags,
            ImageUrl = imageUrl,
            S3Key = attachment.S3Key,
            CreatedAt = now
        };

        dbContext.Stickers.Add(sticker);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created sticker {StickerName} (ID: {StickerId}) in pack {PackId} for server {ServerId}",
            userId, sticker.Name, sticker.Id, request.PackId, request.ServerId);

        return new CreateStickerResponse(
            Id: sticker.Id,
            StickerPackId: sticker.StickerPackId,
            Name: sticker.Name,
            Tags: sticker.Tags,
            ImageUrl: sticker.ImageUrl,
            CreatedAt: sticker.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/sticker-packs/{packId}/stickers", async (
            long serverId,
            long packId,
            CreateStickerRequest request,
            IRequestHandler<CreateStickerCommand, Result<CreateStickerResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateStickerCommand(
                ServerId: serverId,
                PackId: packId,
                Name: request.Name,
                Tags: request.Tags,
                AttachmentId: request.AttachmentId
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateSticker")
        .WithTags("Stickers");
    }
}

public sealed record CreateStickerRequest(
    string Name,
    string? Tags,
    long AttachmentId
);
