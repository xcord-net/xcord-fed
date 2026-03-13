using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Cryptography;

using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Auth;

public sealed record LoginRequest(
    string Email,
    string Password
);

public sealed class LoginHandler(
    AppDbContext dbContext,
    IEncryptionService encryptionService,
    IJwtService jwtService,
    SnowflakeIdGenerator snowflakeGenerator,
    ILogger<LoginHandler> logger,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions)
    : IRequestHandler<LoginRequest, Result<object>>, IValidatable<LoginRequest>
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public Error? Validate(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Error.Validation("VALIDATION_FAILED", "Email is required");

        if (!request.Email.Contains('@'))
            return Error.Validation("VALIDATION_FAILED", "Email must be a valid email address");

        if (string.IsNullOrWhiteSpace(request.Password))
            return Error.Validation("VALIDATION_FAILED", "Password is required");

        return null;
    }

    public async Task<Result<object>> Handle(LoginRequest request, CancellationToken cancellationToken)
    {
        // Find user by EmailHash
        var emailHash = encryptionService.ComputeHmac(request.Email.ToLowerInvariant());
        var emailHashHex = Convert.ToHexString(emailHash);
        var redisKey = $"{redisOptions.Value.ChannelPrefix}:login-attempts:{emailHashHex}";

        // Check brute-force counter BEFORE verifying the password
        var db = redis.GetDatabase();
        var currentCount = (long?)await db.StringGetAsync(redisKey);
        if (currentCount >= MaxFailedAttempts)
        {
            var ttl = await db.KeyTimeToLiveAsync(redisKey);
            var retryAfterSeconds = ttl.HasValue ? (int)Math.Ceiling(ttl.Value.TotalSeconds) : (int)LockoutDuration.TotalSeconds;
            return Error.RateLimited("LOGIN_RATE_LIMITED", retryAfterSeconds.ToString());
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.EmailHash == emailHash, cancellationToken);

        if (user == null)
        {
            // Increment counter to prevent email enumeration via timing
            await IncrementAttemptCounterAsync(db, redisKey);
            return Error.Validation("INVALID_CREDENTIALS", "Invalid email or password");
        }

        // Verify password - offloaded to Task.Run to avoid thread pool starvation
        if (!await Task.Run(() => BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)))
        {
            await IncrementAttemptCounterAsync(db, redisKey);
            return Error.Validation("INVALID_CREDENTIALS", "Invalid email or password");
        }

        // Successful login - clear the brute-force counter
        await db.KeyDeleteAsync(redisKey);

        // Check if account is disabled
        if (user.IsDisabled)
        {
            return Error.Forbidden("ACCOUNT_DISABLED", "Account is disabled");
        }

        // Update last login timestamp
        user.LastLoginAt = DateTimeOffset.UtcNow;

        // If 2FA enabled, generate code and return 2FA required response
        if (user.TwoFactorEnabled)
        {
            var twoFactorCode = GenerateTwoFactorCode();
            var now = DateTimeOffset.UtcNow;

            var twoFactorEntity = new TwoFactorCode
            {
                Id = snowflakeGenerator.NextId(),
                UserId = user.Id,
                Code = twoFactorCode,
                ExpiresAt = now.AddMinutes(10),
                CreatedAt = now
            };

            dbContext.TwoFactorCodes.Add(twoFactorEntity);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Log that 2FA code was generated (code itself is not logged for security)
            logger.LogInformation("2FA code generated for user {Username}", user.Username);

            // Generate a short-lived RSA-signed token that proves the user passed password auth
            var twoFactorToken = jwtService.GenerateTwoFactorToken(user.Id);

            return new TwoFactorRequiredResponse(true, twoFactorToken);
        }

        // Create refresh token (30 days)
        var refreshTokenValue = TokenHelper.GenerateRefreshToken();
        var refreshTokenHash = TokenHelper.HashToken(refreshTokenValue);
        var now2 = DateTimeOffset.UtcNow;

        var refreshToken = new Entities.RefreshToken
        {
            Id = snowflakeGenerator.NextId(),
            TokenHash = refreshTokenHash,
            UserId = user.Id,
            ExpiresAt = now2.AddDays(30),
            CreatedAt = now2
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Generate JWT access token
        var accessToken = jwtService.GenerateAccessToken(user.Id, user.IsAdmin, user.EmailConfirmed, user.IsBot);

        return new LoginResponse(user.Id, user.Username, accessToken, user.EmailConfirmed, refreshTokenValue);
    }

    private static string GenerateTwoFactorCode()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }

    private static async Task IncrementAttemptCounterAsync(IDatabase db, string key)
    {
        var count = await db.StringIncrementAsync(key);
        if (count == 1)
        {
            // First failure - set the TTL so the lockout window starts now
            await db.KeyExpireAsync(key, LockoutDuration);
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/login", async (
                [FromBody] LoginRequest command,
                HttpContext httpContext,
                [FromServices] LoginHandler handler,
                CancellationToken ct) =>
            {
                if (handler is IValidatable<LoginRequest> validatable)
                {
                    var validationError = validatable.Validate(command);
                    if (validationError is not null)
                        return Results.Problem(statusCode: validationError.StatusCode, title: validationError.Code, detail: validationError.Message);
                }

                var result = await handler.Handle(command, ct);

                return result.Match(
                    success =>
                    {
                        // Check if this is a 2FA required response
                        if (success is TwoFactorRequiredResponse twoFactorResponse)
                        {
                            return Results.Ok(twoFactorResponse);
                        }

                        // Otherwise it's a normal login response
                        var loginResponse = (LoginResponse)success;

                        // Set httpOnly cookies for both tokens
                        AuthCookieHelper.SetAccessTokenCookie(httpContext, loginResponse.AccessToken, 15);
                        AuthCookieHelper.SetRefreshTokenCookie(httpContext, loginResponse.RefreshToken);

                        return Results.Ok(new
                        {
                            authenticated = true,
                            userId = loginResponse.UserId,
                            username = loginResponse.Username,
                            emailConfirmed = loginResponse.EmailConfirmed
                        });
                    },
                    error =>
                    {
                        if (error.StatusCode == 429 && error.Code == "LOGIN_RATE_LIMITED"
                            && int.TryParse(error.Message, out var retryAfter))
                        {
                            httpContext.Response.Headers["Retry-After"] = retryAfter.ToString();
                            return Results.Problem(
                                statusCode: 429,
                                title: error.Code,
                                detail: $"Too many failed login attempts. Please wait {retryAfter} second(s) before trying again.");
                        }

                        return Results.Problem(
                            statusCode: error.StatusCode,
                            title: error.Code,
                            detail: error.Message);
                    }
                );
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithName("Login")
            .WithTags("Auth");
    }
}
