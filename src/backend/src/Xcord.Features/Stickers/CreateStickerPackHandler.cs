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

public sealed record CreateStickerPackCommand(
    long ServerId,
    string Name,
    string? Description
);

public sealed record CreateStickerPackResponse(
    long Id,
    long ServerId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt
);

public sealed class CreateStickerPackHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    ILogger<CreateStickerPackHandler> logger)
    : IRequestHandler<CreateStickerPackCommand, Result<CreateStickerPackResponse>>, IValidatable<CreateStickerPackCommand>
{
    public Error? Validate(CreateStickerPackCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be a valid snowflake");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("VALIDATION_ERROR", "Name is required");
        }

        if (request.Name.Length > 100)
        {
            return Error.Validation("VALIDATION_ERROR", "Name must not exceed 100 characters");
        }

        if (request.Description != null && request.Description.Length > 200)
        {
            return Error.Validation("VALIDATION_ERROR", "Description must not exceed 200 characters");
        }

        return null;
    }

    public async Task<Result<CreateStickerPackResponse>> Handle(CreateStickerPackCommand request, CancellationToken cancellationToken)
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

        // Create sticker pack
        var now = DateTimeOffset.UtcNow;
        var stickerPack = new StickerPack
        {
            Id = snowflakeGenerator.NextId(),
            ServerId = request.ServerId,
            Name = request.Name,
            Description = request.Description,
            CreatedAt = now
        };

        dbContext.StickerPacks.Add(stickerPack);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created sticker pack {PackName} (ID: {PackId}) in server {ServerId}",
            userId, stickerPack.Name, stickerPack.Id, request.ServerId);

        return new CreateStickerPackResponse(
            Id: stickerPack.Id,
            ServerId: request.ServerId,
            Name: stickerPack.Name,
            Description: stickerPack.Description,
            CreatedAt: stickerPack.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/sticker-packs", async (
            long serverId,
            CreateStickerPackRequest request,
            IRequestHandler<CreateStickerPackCommand, Result<CreateStickerPackResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateStickerPackCommand(
                ServerId: serverId,
                Name: request.Name,
                Description: request.Description
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateStickerPack")
        .WithTags("Stickers");
    }
}

public sealed record CreateStickerPackRequest(
    string Name,
    string? Description
);
