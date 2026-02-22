using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Emoji;

public sealed record ListEmojisQuery(
    long ServerId
);

public sealed record ListEmojisResponse(
    List<EmojiDto> Emojis
);

public sealed record EmojiDto(
    long Id,
    long ServerId,
    string Name,
    string ImageUrl,
    bool IsAnimated,
    long CreatorId,
    DateTimeOffset CreatedAt
);

public sealed class ListEmojisHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<ListEmojisQuery, Result<ListEmojisResponse>>
{
    public async Task<Result<ListEmojisResponse>> Handle(ListEmojisQuery request, CancellationToken cancellationToken)
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

        // Get all emojis for the server
        var emojiEntities = await dbContext.CustomEmojis
            .AsNoTracking()
            .Where(e => e.ServerId == request.ServerId)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

        var emojis = emojiEntities.Select(e => new EmojiDto(
            e.Id,
            e.ServerId,
            e.Name,
            e.ImageUrl,
            e.IsAnimated,
            e.CreatorId,
            e.CreatedAt
        )).ToList();

        return new ListEmojisResponse(emojis);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/emojis", async (
            long serverId,
            IRequestHandler<ListEmojisQuery, Result<ListEmojisResponse>> handler,
            CancellationToken ct) =>
        {
            var query = new ListEmojisQuery(serverId);
            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListEmojis")
        .WithTags("Emoji");
    }
}
