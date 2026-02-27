using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Admin;

public sealed record DeleteBotCommand(
    long BotId
);

public sealed record DeleteBotResponse(
    long BotId,
    bool Success
);

public sealed class DeleteBotHandler(
    AppDbContext dbContext,
    ILogger<DeleteBotHandler> logger)
    : IRequestHandler<DeleteBotCommand, Result<DeleteBotResponse>>
{
    public async Task<Result<DeleteBotResponse>> Handle(DeleteBotCommand request, CancellationToken cancellationToken)
    {
        var bot = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.BotId, cancellationToken);

        if (bot == null)
        {
            return Error.NotFound("BOT_NOT_FOUND", "Bot user not found");
        }

        if (!bot.IsBot)
        {
            return Error.Validation("NOT_A_BOT", "User is not a bot account");
        }

        if (bot.DeletedAt.HasValue)
        {
            return Error.Conflict("BOT_ALREADY_DELETED", "Bot is already deleted");
        }

        // Soft delete the bot user
        bot.DeletedAt = DateTimeOffset.UtcNow;

        // Revoke all tokens
        var tokens = await dbContext.BotTokens
            .Where(bt => bt.UserId == request.BotId && !bt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Deleted bot user {BotId} ({Username}) and revoked {TokenCount} tokens",
            request.BotId, bot.Username, tokens.Count);

        return new DeleteBotResponse(
            BotId: request.BotId,
            Success: true
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/admin/bots/{botId}", async (
            long botId,
            [FromServices] DeleteBotHandler handler,
            CancellationToken ct) =>
        {
            var command = new DeleteBotCommand(botId);

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAuthorization(Policies.Admin)
        .WithName("DeleteBot")
        .WithTags("Admin", "Bots");
    }
}
