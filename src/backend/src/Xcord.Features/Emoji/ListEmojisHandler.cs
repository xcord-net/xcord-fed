using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

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
    ICurrentUserService currentUserService)
    : IRequestHandler<ListEmojisQuery, Result<ListEmojisResponse>>
{
    public async Task<Result<ListEmojisResponse>> Handle(ListEmojisQuery request, CancellationToken cancellationToken)
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
            return await handler.ExecuteAsync(query, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListEmojis")
        .WithTags("Emoji");
    }
}
