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

public sealed record AssignAgentCommand(
    long BotId,
    string AgentId,
    JsonElement Parameters
);

public sealed record AssignAgentRequest(
    string AgentId,
    JsonElement Parameters
);

public sealed record AssignAgentResponse(
    long BotId,
    string AgentId,
    bool IsRunning
);

public sealed class AssignAgentHandler(
    AppDbContext dbContext,
    BotAgentRegistry agentRegistry,
    BotProcessManager processManager,
    SnowflakeIdGenerator snowflakeGenerator,
    IOptions<InstanceOptions> instanceOptions,
    ILogger<AssignAgentHandler> logger)
    : IRequestHandler<AssignAgentCommand, Result<AssignAgentResponse>>, IValidatable<AssignAgentCommand>
{
    public Error? Validate(AssignAgentCommand request)
    {
        if (request.BotId <= 0)
            return Error.Validation("VALIDATION_ERROR", "Bot ID must be greater than 0");

        if (string.IsNullOrWhiteSpace(request.AgentId))
            return Error.Validation("VALIDATION_ERROR", "Agent ID is required");

        return null;
    }

    public async Task<Result<AssignAgentResponse>> Handle(AssignAgentCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.BotId, cancellationToken);

        if (user == null || !user.IsBot)
            return Error.NotFound("BOT_NOT_FOUND", "Bot user not found");

        var manifest = agentRegistry.GetById(request.AgentId);
        if (manifest == null)
            return Error.NotFound("AGENT_NOT_FOUND", $"Bot agent '{request.AgentId}' not found");

        // Revoke any existing agent token for this bot
        var existingAgentTokens = await dbContext.BotTokens
            .Where(t => t.UserId == request.BotId && !t.IsRevoked && t.AgentId != null)
            .ToListAsync(cancellationToken);

        foreach (var old in existingAgentTokens)
        {
            old.IsRevoked = true;
            processManager.StopBot(old.Id);
        }

        // Find or update the primary bot token with agent assignment
        var primaryToken = await dbContext.BotTokens
            .Where(t => t.UserId == request.BotId && !t.IsRevoked && t.AgentId == null)
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        // Generate a dedicated agent token
        var rawToken = GenerateBotToken();
        var tokenHash = HashToken(rawToken);

        var agentToken = new BotToken
        {
            Id = snowflakeGenerator.NextId(),
            TokenHash = tokenHash,
            UserId = request.BotId,
            Name = $"agent-{request.AgentId}",
            Roles = primaryToken?.Roles ?? 0,
            IsRevoked = false,
            AgentId = request.AgentId,
            AgentConfigJson = request.Parameters.ValueKind == JsonValueKind.Undefined
                ? null
                : JsonSerializer.Serialize(request.Parameters),
            CreatedAt = now,
            LastUsedAt = null
        };

        dbContext.BotTokens.Add(agentToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Start the bot process
        var baseUrl = $"https://{instanceOptions.Value.Domain}";
        var started = await processManager.StartBotAsync(
            agentToken.Id,
            request.AgentId,
            rawToken,
            baseUrl,
            request.Parameters.ValueKind == JsonValueKind.Undefined ? null : request.Parameters);

        logger.LogInformation(
            "Assigned agent {AgentId} to bot {BotId} (token {TokenId}), started={Started}",
            request.AgentId, request.BotId, agentToken.Id, started);

        return new AssignAgentResponse(request.BotId, request.AgentId, started);
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
        return app.MapPost("/api/v1/admin/bots/{botId:long}/assign-agent", async (
            long botId,
            [FromBody] AssignAgentRequest requestBody,
            [FromServices] AssignAgentHandler handler,
            CancellationToken ct) =>
        {
            var command = new AssignAgentCommand(botId, requestBody.AgentId, requestBody.Parameters);
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAuthorization(Policies.Admin)
        .WithName("AssignBotAgent")
        .WithTags("Admin", "Bots");
    }
}
