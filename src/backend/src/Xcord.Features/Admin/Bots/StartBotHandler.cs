using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services.Bots;

namespace Xcord.Features.Admin;

public sealed record StartBotCommand(long BotId);

public sealed record StartBotResponse(
    long BotId,
    string AgentId,
    bool IsRunning
);

public sealed class StartBotHandler(
    AppDbContext dbContext,
    BotAgentRegistry agentRegistry,
    BotProcessManager processManager,
    SnowflakeIdGenerator snowflakeGenerator,
    IOptions<InstanceOptions> instanceOptions,
    ILogger<StartBotHandler> logger)
    : IRequestHandler<StartBotCommand, Result<StartBotResponse>>
{
    public async Task<Result<StartBotResponse>> Handle(StartBotCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.BotId, cancellationToken);

        if (user == null || !user.IsBot)
            return Error.NotFound("BOT_NOT_FOUND", "Bot user not found");

        // Find the most recent non-revoked agent token for this bot
        var agentToken = await dbContext.BotTokens
            .Where(t => t.UserId == request.BotId && !t.IsRevoked && t.AgentId != null)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (agentToken == null)
            return Error.Validation("NO_AGENT_ASSIGNED", "No agent is assigned to this bot. Use assign-agent first.");

        var manifest = agentRegistry.GetById(agentToken.AgentId!);
        if (manifest == null)
            return Error.NotFound("AGENT_NOT_FOUND", $"Bot agent '{agentToken.AgentId}' not found");

        if (processManager.IsRunning(agentToken.Id))
            return Error.Conflict("BOT_ALREADY_RUNNING", "Bot is already running");

        // Generate a new raw token for this process start (revoke old, issue new)
        agentToken.IsRevoked = true;

        var now = DateTimeOffset.UtcNow;
        var rawToken = GenerateBotToken();
        var tokenHash = HashToken(rawToken);

        var newAgentToken = new BotToken
        {
            Id = snowflakeGenerator.NextId(),
            TokenHash = tokenHash,
            UserId = request.BotId,
            Name = agentToken.Name,
            Roles = agentToken.Roles,
            IsRevoked = false,
            AgentId = agentToken.AgentId,
            AgentConfigJson = agentToken.AgentConfigJson,
            CreatedAt = now,
            LastUsedAt = null
        };

        dbContext.BotTokens.Add(newAgentToken);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        JsonElement? parameters = null;
        if (agentToken.AgentConfigJson != null)
        {
            try
            {
                parameters = JsonSerializer.Deserialize<JsonElement>(agentToken.AgentConfigJson);
            }
            catch
            {
                // ignore malformed config
            }
        }

        var baseUrl = $"https://{instanceOptions.Value.Domain}";
        var started = await processManager.StartBotAsync(
            newAgentToken.Id,
            agentToken.AgentId!,
            rawToken,
            baseUrl,
            parameters);

        logger.LogInformation(
            "Started bot {BotId} with agent {AgentId} (token {TokenId})",
            request.BotId, agentToken.AgentId, newAgentToken.Id);

        return new StartBotResponse(request.BotId, agentToken.AgentId!, started);
    }

    private static string GenerateBotToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string rawToken)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/admin/bots/{botId:long}/start", async (
            long botId,
            [FromServices] StartBotHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new StartBotCommand(botId), ct))
            .RequireAuthorization(Policies.Admin)
            .WithName("StartBot")
            .WithTags("Admin", "Bots");
    }
}
