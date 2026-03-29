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

public sealed record UpdateAgentConfigCommand(
    long BotId,
    JsonElement Parameters
);

public sealed record UpdateAgentConfigRequest(
    JsonElement Parameters
);

public sealed record UpdateAgentConfigResponse(
    long BotId,
    string AgentId,
    bool IsRunning
);

public sealed class UpdateAgentConfigHandler(
    AppDbContext dbContext,
    BotAgentRegistry agentRegistry,
    BotProcessManager processManager,
    SnowflakeIdGenerator snowflakeGenerator,
    IOptions<InstanceOptions> instanceOptions,
    ILogger<UpdateAgentConfigHandler> logger)
    : IRequestHandler<UpdateAgentConfigCommand, Result<UpdateAgentConfigResponse>>
{
    public async Task<Result<UpdateAgentConfigResponse>> Handle(UpdateAgentConfigCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.BotId, cancellationToken);

        if (user == null || !user.IsBot)
            return Error.NotFound("BOT_NOT_FOUND", "Bot user not found");

        var agentToken = await dbContext.BotTokens
            .Where(t => t.UserId == request.BotId && !t.IsRevoked && t.AgentId != null)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (agentToken == null)
            return Error.Validation("NO_AGENT_ASSIGNED", "No agent is assigned to this bot");

        var manifest = agentRegistry.GetById(agentToken.AgentId!);
        if (manifest == null)
            return Error.NotFound("AGENT_NOT_FOUND", $"Bot agent '{agentToken.AgentId}' not found");

        var wasRunning = processManager.IsRunning(agentToken.Id);

        // Stop the running process if active
        if (wasRunning)
        {
            processManager.StopBot(agentToken.Id);
        }

        // Update the config - revoke old token, create new one with updated config
        agentToken.IsRevoked = true;

        var configJson = request.Parameters.ValueKind == JsonValueKind.Undefined
            ? null
            : JsonSerializer.Serialize(request.Parameters);

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
            AgentConfigJson = configJson,
            CreatedAt = now,
            LastUsedAt = null
        };

        dbContext.BotTokens.Add(newAgentToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var isRunning = false;

        // Restart with new config if it was running
        if (wasRunning)
        {
            var baseUrl = $"https://{instanceOptions.Value.Domain}";
            isRunning = await processManager.StartBotAsync(
                newAgentToken.Id,
                agentToken.AgentId!,
                rawToken,
                baseUrl,
                request.Parameters.ValueKind == JsonValueKind.Undefined ? null : request.Parameters);
        }

        logger.LogInformation(
            "Updated agent config for bot {BotId} (agent {AgentId}), restarted={Restarted}",
            request.BotId, agentToken.AgentId, wasRunning);

        return new UpdateAgentConfigResponse(request.BotId, agentToken.AgentId!, isRunning);
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
        return app.MapPatch("/api/v1/admin/bots/{botId:long}/agent-config", async (
            long botId,
            [FromBody] UpdateAgentConfigRequest requestBody,
            [FromServices] UpdateAgentConfigHandler handler,
            CancellationToken ct) =>
        {
            var command = new UpdateAgentConfigCommand(botId, requestBody.Parameters);
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAuthorization(Policies.Admin)
        .WithName("UpdateBotAgentConfig")
        .WithTags("Admin", "Bots");
    }
}
