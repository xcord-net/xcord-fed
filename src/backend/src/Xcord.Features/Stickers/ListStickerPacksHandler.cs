using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

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
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<ListStickerPacksQuery, Result<ListStickerPacksResponse>>
{
    public async Task<Result<ListStickerPacksResponse>> Handle(ListStickerPacksQuery request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

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
            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListStickerPacks")
        .WithTags("Stickers");
    }
}
