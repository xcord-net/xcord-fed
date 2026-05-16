using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Admin;

public sealed record ListBotTokensQuery(
    long BotId
);

public sealed record ListBotTokensResponse(
    BotTokenMetadataDto[] Tokens
);

public sealed record BotTokenMetadataDto(
    long TokenId,
    string TokenName,
    long Roles,
    bool IsRevoked,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt
);

public sealed class ListBotTokensHandler(
    AppDbContext dbContext)
    : IRequestHandler<ListBotTokensQuery, Result<ListBotTokensResponse>>
{
    public async Task<Result<ListBotTokensResponse>> Handle(ListBotTokensQuery request, CancellationToken cancellationToken)
    {
        // Verify bot exists
        var botExists = await dbContext.Users
            .AnyAsync(u => u.Id == request.BotId && u.IsBot, cancellationToken);

        if (!botExists)
        {
            return Error.NotFound("BOT_NOT_FOUND", "Bot user not found");
        }

        var tokens = await dbContext.BotTokens
            .Where(bt => bt.UserId == request.BotId)
            .OrderByDescending(bt => bt.CreatedAt)
            .Select(bt => new BotTokenMetadataDto(
                bt.Id,
                bt.Name,
                bt.Roles,
                bt.IsRevoked,
                bt.CreatedAt,
                bt.LastUsedAt
            ))
            .ToArrayAsync(cancellationToken);

        return new ListBotTokensResponse(tokens);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/admin/bots/{botId}/tokens", async (
            long botId,
            [FromServices] ListBotTokensHandler handler,
            CancellationToken ct) =>
        {
            var query = new ListBotTokensQuery(botId);

            return await handler.ExecuteAsync(query, ct).ConfigureAwait(false);
        })
        .RequireAuthorization(Policies.Admin)
        .WithName("ListBotTokens")
        .WithTags("Admin", "Bots");
    }
}
