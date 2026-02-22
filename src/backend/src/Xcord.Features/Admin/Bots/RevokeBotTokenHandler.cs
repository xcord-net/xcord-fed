using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Admin.Bots;

public sealed record RevokeBotTokenCommand(
    long BotId,
    long TokenId
);

public sealed record RevokeBotTokenResponse(
    long TokenId,
    bool Success
);

public sealed class RevokeBotTokenHandler(
    AppDbContext dbContext,
    ILogger<RevokeBotTokenHandler> logger)
    : IRequestHandler<RevokeBotTokenCommand, Result<RevokeBotTokenResponse>>
{
    public async Task<Result<RevokeBotTokenResponse>> Handle(RevokeBotTokenCommand request, CancellationToken cancellationToken)
    {
        var botToken = await dbContext.BotTokens
            .FirstOrDefaultAsync(bt => bt.Id == request.TokenId && bt.UserId == request.BotId, cancellationToken);

        if (botToken == null)
        {
            return Error.NotFound("BOT_TOKEN_NOT_FOUND", "Bot token not found for this bot");
        }

        if (botToken.IsRevoked)
        {
            return Error.Conflict("BOT_TOKEN_ALREADY_REVOKED", "Bot token is already revoked");
        }

        botToken.IsRevoked = true;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Revoked bot token {TokenId} ({TokenName}) for bot {BotId}",
            botToken.Id, botToken.Name, request.BotId);

        return new RevokeBotTokenResponse(
            TokenId: botToken.Id,
            Success: true
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/admin/bots/{botId}/tokens/{tokenId}", async (
            long botId,
            long tokenId,
            [FromServices] RevokeBotTokenHandler handler,
            CancellationToken ct) =>
        {
            var command = new RevokeBotTokenCommand(
                BotId: botId,
                TokenId: tokenId
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAuthorization(Policies.Admin)
        .WithName("RevokeBotToken")
        .WithTags("Admin", "Bots");
    }
}
