using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Xcord.Entities;
using Xcord.Features.Auth;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Api;

public sealed record SeedUserRequest(
    string Username,
    string DisplayName,
    string Email,
    string Password,
    bool Admin = false
);

public static class TestSeedEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/test/seed-user", async (
                [FromBody] SeedUserRequest request,
                HttpContext httpContext,
                CancellationToken ct) =>
            {
                var services = httpContext.RequestServices;
                var config = services.GetRequiredService<IConfiguration>();
                var logger = services.GetRequiredService<ILogger<Program>>();

                // Verify X-Test-Key header
                var expectedKey = config["TestSeed:Key"];
                if (string.IsNullOrEmpty(expectedKey))
                    return Results.Problem(statusCode: 403, title: "FORBIDDEN", detail: "TestSeed:Key is not configured");

                var providedKey = httpContext.Request.Headers["X-Test-Key"].FirstOrDefault();
                if (providedKey != expectedKey)
                    return Results.Problem(statusCode: 403, title: "FORBIDDEN", detail: "Invalid or missing X-Test-Key header");

                var dbContext = services.GetRequiredService<AppDbContext>();
                var encryptionService = services.GetRequiredService<IEncryptionService>();
                var jwtService = services.GetRequiredService<IJwtService>();
                var snowflakeGenerator = services.GetRequiredService<SnowflakeIdGenerator>();
                var authOptions = services.GetRequiredService<IOptions<AuthOptions>>().Value;

                var now = DateTimeOffset.UtcNow;

                // Idempotency: if username already exists, log in instead of creating
                var existingUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username && u.DeletedAt == null, ct);

                if (existingUser is not null)
                {
                    // Verify password matches to prevent misuse
                    var passwordMatches = await Task.Run(
                        () => BCrypt.Net.BCrypt.Verify(request.Password, existingUser.PasswordHash), ct);

                    if (!passwordMatches)
                        return Results.Problem(statusCode: 409, title: "CONFLICT",
                            detail: "User already exists but password does not match");

                    logger.LogInformation("TestSeed: returning existing user {Username}", existingUser.Username);

                    // Issue new refresh token for existing user
                    var existingRefreshTokenValue = TokenHelper.GenerateToken();
                    var existingRefreshTokenHash = TokenHelper.HashToken(existingRefreshTokenValue);
                    var existingRefreshToken = new Entities.RefreshToken
                    {
                        Id = snowflakeGenerator.NextId(),
                        TokenHash = existingRefreshTokenHash,
                        UserId = existingUser.Id,
                        ExpiresAt = now.AddDays(30),
                        CreatedAt = now
                    };
                    dbContext.RefreshTokens.Add(existingRefreshToken);
                    await dbContext.SaveChangesAsync(ct);

                    var existingAccessToken = jwtService.GenerateAccessToken(
                        existingUser.Id, existingUser.IsAdmin, existingUser.EmailConfirmed, existingUser.IsBot);

                    AuthCookieHelper.SetAccessTokenCookie(httpContext, existingAccessToken, 15);
                    AuthCookieHelper.SetRefreshTokenCookie(httpContext, existingRefreshTokenValue);

                    return Results.Ok(new
                    {
                        userId = existingUser.Id,
                        username = existingUser.Username,
                        accessToken = existingAccessToken,
                        refreshToken = existingRefreshTokenValue
                    });
                }

                // Hash password - offloaded to Task.Run to avoid thread pool starvation
                var passwordHash = await Task.Run(
                    () => BCrypt.Net.BCrypt.HashPassword(request.Password, authOptions.BcryptWorkFactor), ct);

                // Encrypt email and compute blind index
                var encryptedEmail = encryptionService.Encrypt(request.Email.ToLowerInvariant());
                var emailHash = encryptionService.ComputeHmac(request.Email.ToLowerInvariant());

                // Create user with confirmed email
                var userId = snowflakeGenerator.NextId();
                var user = new User
                {
                    Id = userId,
                    Username = request.Username,
                    DisplayName = request.DisplayName,
                    Email = encryptedEmail,
                    EmailHash = emailHash,
                    PasswordHash = passwordHash,
                    EmailConfirmed = true,
                    TwoFactorEnabled = false,
                    IsAdmin = request.Admin,
                    IsBot = false,
                    IsDisabled = false,
                    CreatedAt = now,
                    LastLoginAt = now
                };
                dbContext.Users.Add(user);

                // Create refresh token (30 days)
                var refreshTokenValue = TokenHelper.GenerateToken();
                var refreshTokenHash = TokenHelper.HashToken(refreshTokenValue);
                var refreshToken = new Entities.RefreshToken
                {
                    Id = snowflakeGenerator.NextId(),
                    TokenHash = refreshTokenHash,
                    UserId = userId,
                    ExpiresAt = now.AddDays(30),
                    CreatedAt = now
                };
                dbContext.RefreshTokens.Add(refreshToken);

                await dbContext.SaveChangesAsync(ct);

                logger.LogInformation("TestSeed: created user {Username}", request.Username);

                // Generate JWT access token
                var accessToken = jwtService.GenerateAccessToken(userId, user.IsAdmin, user.EmailConfirmed, user.IsBot);

                AuthCookieHelper.SetAccessTokenCookie(httpContext, accessToken, 15);
                AuthCookieHelper.SetRefreshTokenCookie(httpContext, refreshTokenValue);

                return Results.Ok(new
                {
                    userId,
                    username = user.Username,
                    accessToken,
                    refreshToken = refreshTokenValue
                });
            })
            .AllowAnonymous()
            .WithName("TestSeedUser")
            .WithTags("Test");

        // One-click sign-in for the local dev stack, behind the "Dev login as
        // admin" button on the login page. Takes no body and no X-Test-Key:
        // the browser cannot hold a secret, so the gate is that this route is
        // not mapped at all unless TestSeed:Key is configured (see Program.cs).
        app.MapPost("/api/v1/test/dev-login", async (
                HttpContext httpContext,
                CancellationToken ct) =>
            {
                var services = httpContext.RequestServices;
                var logger = services.GetRequiredService<ILogger<Program>>();
                var dbContext = services.GetRequiredService<AppDbContext>();
                var jwtService = services.GetRequiredService<IJwtService>();
                var snowflakeGenerator = services.GetRequiredService<SnowflakeIdGenerator>();

                var now = DateTimeOffset.UtcNow;

                // Oldest admin. On a hub-provisioned instance that is the owner
                // seeded during provisioning by the hub's StartApiContainerStep.
                var admin = await dbContext.Users
                    .Where(u => u.IsAdmin && !u.IsDisabled && u.DeletedAt == null)
                    .OrderBy(u => u.Id)
                    .FirstOrDefaultAsync(ct);

                if (admin is null)
                    return Results.Problem(statusCode: 404, title: "NOT_FOUND",
                        detail: "No admin user exists on this instance");

                var refreshTokenValue = TokenHelper.GenerateToken();
                dbContext.RefreshTokens.Add(new Entities.RefreshToken
                {
                    Id = snowflakeGenerator.NextId(),
                    TokenHash = TokenHelper.HashToken(refreshTokenValue),
                    UserId = admin.Id,
                    ExpiresAt = now.AddDays(30),
                    CreatedAt = now
                });
                await dbContext.SaveChangesAsync(ct);

                var accessToken = jwtService.GenerateAccessToken(
                    admin.Id, admin.IsAdmin, admin.EmailConfirmed, admin.IsBot);

                AuthCookieHelper.SetAccessTokenCookie(httpContext, accessToken, 15);
                AuthCookieHelper.SetRefreshTokenCookie(httpContext, refreshTokenValue);

                logger.LogWarning("TestSeed: dev-login issued for admin {Username}", admin.Username);

                return Results.Ok(new
                {
                    authenticated = true,
                    userId = admin.Id.ToString(),
                    username = admin.Username,
                    emailConfirmed = admin.EmailConfirmed
                });
            })
            .AllowAnonymous()
            .WithName("TestDevLogin")
            .WithTags("Test");
    }
}
