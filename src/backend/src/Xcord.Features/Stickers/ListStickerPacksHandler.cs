using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Stickers;

public sealed record ListStickerPacksQuery(
    long ServerId
);

public sealed record ListStickerPacksResponse(
    List<StickerPackDto> StickerPacks
);

public sealed record StickerPackDto(
    long Id,
    long ServerId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    List<StickerDto> Stickers
);

public sealed record StickerDto(
    long Id,
    string Name,
    string? Tags,
    string ImageUrl,
    DateTimeOffset CreatedAt
);

public sealed class ListStickerPacksHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListStickerPacksQuery, Result<ListStickerPacksResponse>>
{
    public async Task<Result<ListStickerPacksResponse>> Handle(ListStickerPacksQuery request, CancellationToken cancellationToken)
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

        // Check membership
        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == request.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_MEMBER", "You are not a member of this server");
        }

        // Get all sticker packs for the server with their stickers
        var stickerPacks = await dbContext.StickerPacks
            .AsNoTracking()
            .Include(p => p.Stickers)
            .Where(p => p.ServerId == request.ServerId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        // Map to DTOs
        var result = stickerPacks.Select(p => new StickerPackDto(
            p.Id,
            p.ServerId!.Value,
            p.Name,
            p.Description,
            p.CreatedAt,
            p.Stickers.Select(s => new StickerDto(
                s.Id,
                s.Name,
                s.Tags,
                s.ImageUrl,
                s.CreatedAt
            )).ToList()
        )).ToList();

        return new ListStickerPacksResponse(result);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/sticker-packs", async (
            long serverId,
            IRequestHandler<ListStickerPacksQuery, Result<ListStickerPacksResponse>> handler,
            CancellationToken ct) =>
        {
            var query = new ListStickerPacksQuery(serverId);
            return await handler.ExecuteAsync(query, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListStickerPacks")
        .WithTags("Stickers");
    }
}
