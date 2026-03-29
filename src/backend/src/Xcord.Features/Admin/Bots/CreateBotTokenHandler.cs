using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Admin;

public sealed record CreateBotTokenCommand(
    long BotId,
    string TokenName,
    long Roles
);

public sealed record CreateBotTokenResponse(
    long TokenId,
    string TokenName,
    string RawToken,
    long Roles,
    DateTimeOffset CreatedAt
);

public sealed record CreateBotTokenRequest(
    string TokenName,
    long Roles
);

public sealed class CreateBotTokenHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ILogger<CreateBotTokenHandler> logger)
    : IRequestHandler<CreateBotTokenCommand, Result<CreateBotTokenResponse>>, IValidatable<CreateBotTokenCommand>
{
    public Error? Validate(CreateBotTokenCommand request)
    {
        if (request.BotId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Bot ID must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(request.TokenName))
        {
            return Error.Validation("VALIDATION_ERROR", "Token name is required");
        }

        if (request.TokenName.Length > 100)
        {
            return Error.Validation("VALIDATION_ERROR", "Token name cannot exceed 100 characters");
        }

        if (request.Roles < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Roles must be a non-negative value");
        }

        return null;
    }

    public async Task<Result<CreateBotTokenResponse>> Handle(CreateBotTokenCommand request, CancellationToken cancellationToken)
    {
        // Verify bot user exists and is actually a bot
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.BotId, cancellationToken);

        if (user == null)
        {
            return Error.NotFound("BOT_NOT_FOUND", "Bot user not found");
        }

        if (!user.IsBot)
        {
            return Error.Validation("NOT_A_BOT", "User is not a bot account");
        }

        var now = DateTimeOffset.UtcNow;

        // Generate bot token
        var rawToken = GenerateBotToken();
        var tokenHash = HashToken(rawToken);

        var botToken = new BotToken
        {
            Id = snowflakeGenerator.NextId(),
            TokenHash = tokenHash,
            UserId = request.BotId,
            Name = request.TokenName,
            Roles = request.Roles,
            IsRevoked = false,
            CreatedAt = now,
            LastUsedAt = null
        };

        dbContext.BotTokens.Add(botToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created bot token {TokenName} (ID: {TokenId}) for bot {BotId}",
            request.TokenName, botToken.Id, request.BotId);

        return new CreateBotTokenResponse(
            TokenId: botToken.Id,
            TokenName: botToken.Name,
            RawToken: rawToken,
            Roles: botToken.Roles,
            CreatedAt: botToken.CreatedAt
        );
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
        return app.MapPost("/api/v1/admin/bots/{botId}/tokens", async (
            long botId,
            [FromBody] CreateBotTokenRequest requestBody,
            [FromServices] CreateBotTokenHandler handler,
            CancellationToken ct) =>
        {
            var command = new CreateBotTokenCommand(
                BotId: botId,
                TokenName: requestBody.TokenName,
                Roles: requestBody.Roles
            );

            return await handler.ExecuteAsync(command, ct, success => Results.Created($"/api/v1/admin/bots/{command.BotId}/tokens/{success.TokenId}", success));
        })
        .RequireAuthorization(Policies.Admin)
        .WithName("CreateBotToken")
        .WithTags("Admin", "Bots");
    }
}
