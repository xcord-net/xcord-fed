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
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Admin;

public sealed record CreateBotCommand(
    string Username,
    string DisplayName,
    string TokenName,
    long Permissions
);

public sealed class CreateBotHandler(
    AppDbContext dbContext,
    IEncryptionService encryptionService,
    SnowflakeIdGenerator snowflakeGenerator,
    ILogger<CreateBotHandler> logger)
    : IRequestHandler<CreateBotCommand, Result<CreateBotResponse>>, IValidatable<CreateBotCommand>
{
    public Error? Validate(CreateBotCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return Error.Validation("VALIDATION_ERROR", "Username is required");
        }

        if (request.Username.Length > 32)
        {
            return Error.Validation("VALIDATION_ERROR", "Username must not exceed 32 characters");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Username, "^[a-zA-Z0-9_]+$"))
        {
            return Error.Validation("VALIDATION_ERROR", "Username can only contain letters, numbers, and underscores");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Error.Validation("VALIDATION_ERROR", "Display name is required");
        }

        if (request.DisplayName.Length > 32)
        {
            return Error.Validation("VALIDATION_ERROR", "Display name must not exceed 32 characters");
        }

        if (string.IsNullOrWhiteSpace(request.TokenName))
        {
            return Error.Validation("VALIDATION_ERROR", "Token name is required");
        }

        if (request.TokenName.Length > 100)
        {
            return Error.Validation("VALIDATION_ERROR", "Token name must not exceed 100 characters");
        }

        if (request.Permissions < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Permissions must be a non-negative value");
        }

        return null;
    }

    public async Task<Result<CreateBotResponse>> Handle(CreateBotCommand request, CancellationToken cancellationToken)
    {
        // Check if username already exists
        var usernameExists = await dbContext.Users
            .AnyAsync(u => u.Username == request.Username, cancellationToken);

        if (usernameExists)
        {
            return Error.Conflict("USERNAME_TAKEN", "Username is already taken");
        }

        var now = DateTimeOffset.UtcNow;

        // Generate a random email for the bot (bots don't use email, but schema requires it)
        var botEmail = $"bot-{Guid.NewGuid()}@internal.xcord";
        var encryptedEmail = encryptionService.Encrypt(botEmail);
        var emailHash = encryptionService.ComputeHmac(botEmail);

        // Create bot user account
        var userId = snowflakeGenerator.NextId();
        var user = new User
        {
            Id = userId,
            Username = request.Username,
            DisplayName = request.DisplayName,
            Email = encryptedEmail,
            EmailHash = emailHash,
            PasswordHash = string.Empty, // Bots don't have passwords
            EmailConfirmed = true, // Bots are always "confirmed"
            TwoFactorEnabled = false,
            IsAdmin = false,
            IsBot = true,
            IsDisabled = false,
            CreatedAt = now,
            LastLoginAt = null
        };

        dbContext.Users.Add(user);

        // Generate bot token
        var rawToken = GenerateBotToken();
        var tokenHash = HashToken(rawToken);

        var botToken = new BotToken
        {
            Id = snowflakeGenerator.NextId(),
            TokenHash = tokenHash,
            UserId = userId,
            Name = request.TokenName,
            Permissions = request.Permissions,
            IsRevoked = false,
            CreatedAt = now,
            LastUsedAt = null
        };

        dbContext.BotTokens.Add(botToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created bot user {Username} (ID: {UserId}) with token {TokenName} (ID: {TokenId})",
            request.Username, userId, request.TokenName, botToken.Id);

        return new CreateBotResponse(
            UserId: userId,
            Username: user.Username,
            DisplayName: user.DisplayName,
            TokenId: botToken.Id,
            TokenName: botToken.Name,
            RawToken: rawToken, // Return raw token only once
            Permissions: botToken.Permissions,
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
        return app.MapPost("/api/v1/admin/bots", async (
            [FromBody] CreateBotCommand command,
            [FromServices] CreateBotHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(command, ct, success => Results.Created($"/api/v1/admin/bots/{success.UserId}", success)))
            .RequireAuthorization(Policies.Admin)
            .WithName("CreateBot")
            .WithTags("Admin", "Bots");
    }
}
