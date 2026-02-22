using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Admin.Bots;

public sealed record ListBotsQuery;

public sealed class ListBotsHandler(
    AppDbContext dbContext)
    : IRequestHandler<ListBotsQuery, Result<ListBotsResponse>>
{
    public async Task<Result<ListBotsResponse>> Handle(ListBotsQuery request, CancellationToken cancellationToken)
    {
        var botTokens = await dbContext.BotTokens
            .Include(bt => bt.User)
            .OrderByDescending(bt => bt.CreatedAt)
            .Select(bt => new BotTokenDto(
                bt.Id,
                bt.Name,
                bt.UserId,
                bt.User.Username,
                bt.Permissions,
                bt.IsRevoked,
                bt.CreatedAt,
                bt.LastUsedAt
            ))
            .ToArrayAsync(cancellationToken);

        return new ListBotsResponse(botTokens);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/admin/bots", async (
            [FromServices] ListBotsHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new ListBotsQuery(), ct);
        })
        .RequireAuthorization(Policies.Admin)
        .WithName("ListBots")
        .WithTags("Admin", "Bots");
    }
}
