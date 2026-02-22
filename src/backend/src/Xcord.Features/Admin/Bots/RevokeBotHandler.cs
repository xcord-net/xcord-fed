using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Admin.Bots;

public sealed record RevokeBotCommand(
    long TokenId
);

public sealed class RevokeBotHandler(
    AppDbContext dbContext,
    ILogger<RevokeBotHandler> logger)
    : IRequestHandler<RevokeBotCommand, Result<RevokeBotResponse>>
{
    public async Task<Result<RevokeBotResponse>> Handle(RevokeBotCommand request, CancellationToken cancellationToken)
    {
        var botToken = await dbContext.BotTokens
            .FirstOrDefaultAsync(bt => bt.Id == request.TokenId, cancellationToken);

        if (botToken == null)
        {
            return Error.NotFound("BOT_TOKEN_NOT_FOUND", "Bot token not found");
        }

        if (botToken.IsRevoked)
        {
            return Error.Conflict("BOT_TOKEN_ALREADY_REVOKED", "Bot token is already revoked");
        }

        botToken.IsRevoked = true;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Revoked bot token {TokenId} ({TokenName})", botToken.Id, botToken.Name);

        return new RevokeBotResponse(
            TokenId: botToken.Id,
            Success: true
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/admin/bots/{tokenId:long}", async (
            long tokenId,
            [FromServices] RevokeBotHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new RevokeBotCommand(tokenId), ct);
        })
        .RequireAuthorization(Policies.Admin)
        .WithName("RevokeBot")
        .WithTags("Admin", "Bots");
    }
}
