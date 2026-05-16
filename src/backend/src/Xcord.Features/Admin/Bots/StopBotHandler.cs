using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services.Bots;

namespace Xcord.Features.Admin;

public sealed record StopBotCommand(long BotId);

public sealed record StopBotResponse(
    long BotId,
    bool Stopped
);

public sealed class StopBotHandler(
    AppDbContext dbContext,
    BotProcessManager processManager,
    ILogger<StopBotHandler> logger)
    : IRequestHandler<StopBotCommand, Result<StopBotResponse>>
{
    public async Task<Result<StopBotResponse>> Handle(StopBotCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.BotId, cancellationToken);

        if (user == null || !user.IsBot)
            return Error.NotFound("BOT_NOT_FOUND", "Bot user not found");

        // Find running agent token(s) for this bot
        var agentTokens = await dbContext.BotTokens
            .Where(t => t.UserId == request.BotId && !t.IsRevoked && t.AgentId != null)
            .ToListAsync(cancellationToken);

        var stopped = false;
        foreach (var token in agentTokens)
        {
            if (processManager.IsRunning(token.Id))
            {
                processManager.StopBot(token.Id);
                stopped = true;
            }

            // Revoke the token so it cannot be reused
            token.IsRevoked = true;
        }

        if (agentTokens.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Stopped bot {BotId}, stopped={Stopped}", request.BotId, stopped);

        return new StopBotResponse(request.BotId, stopped);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/admin/bots/{botId:long}/stop", async (
            long botId,
            [FromServices] StopBotHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new StopBotCommand(botId), ct))
            .RequireAuthorization(Policies.Admin)
            .WithName("StopBot")
            .WithTags("Admin", "Bots");
    }
}
