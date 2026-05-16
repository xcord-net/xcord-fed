using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Auth;

public sealed record TwoFactorVerifyRequest(
    string Code,
    string TwoFactorToken
);

public sealed record TwoFactorVerifyResponse(
    long UserId,
    string Username,
    string AccessToken,
    bool EmailConfirmed,
    string RefreshToken
);

public sealed class TwoFactorVerifyHandler(
    AppDbContext dbContext,
    IJwtService jwtService,
    SnowflakeIdGenerator snowflakeGenerator)
    : IRequestHandler<TwoFactorVerifyRequest, Result<TwoFactorVerifyResponse>>, IValidatable<TwoFactorVerifyRequest>
{
    public Error? Validate(TwoFactorVerifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Error.Validation("VALIDATION_FAILED", "Code is required");

        if (string.IsNullOrWhiteSpace(request.TwoFactorToken))
            return Error.Validation("VALIDATION_FAILED", "Two-factor token is required");

        // Allow 6-digit OTP codes or backup codes (8 alphanumeric, or xxxx-xxxx format)
        var normalized = request.Code.Replace("-", "");
        var isOtp = request.Code.Length == 6 && request.Code.All(char.IsDigit);
        var isBackupCode = normalized.Length == 8 && normalized.All(char.IsLetterOrDigit);

        if (!isOtp && !isBackupCode)
            return Error.Validation("VALIDATION_FAILED", "Code must be a 6-digit OTP or an 8-character backup code");

        return null;
    }

    private const int MaxCumulativeTwoFactorFailures = 10;
    private static readonly TimeSpan TwoFactorLockoutDuration = TimeSpan.FromMinutes(30);

    public async Task<Result<TwoFactorVerifyResponse>> Handle(TwoFactorVerifyRequest request, CancellationToken cancellationToken)
    {
        // Validate the RSA-signed 2FA token and extract userId
        var tokenResult = jwtService.ValidateTwoFactorToken(request.TwoFactorToken);
        if (tokenResult.IsFailure)
        {
            return tokenResult.Error;
        }

        var userId = tokenResult.Value;

        // Check cumulative 2FA failure lockout before attempting verification
        var user = await dbContext.Users.FindAsync(new object[] { userId }, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        if (user.TwoFactorLockedAt != null)
        {
            var lockExpiry = user.TwoFactorLockedAt.Value.Add(TwoFactorLockoutDuration);
            if (DateTimeOffset.UtcNow < lockExpiry)
            {
                return Error.Forbidden("TWO_FACTOR_LOCKED",
                    "Account is temporarily locked due to too many failed 2FA attempts. Please try again later.");
            }

            // Lockout has expired - reset counters
            user.TwoFactorFailureCount = 0;
            user.TwoFactorLockedAt = null;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Determine whether this is a backup code attempt
        var normalized = request.Code.Replace("-", "");
        var isBackupCode = !(request.Code.Length == 6 && request.Code.All(char.IsDigit));

        Result<TwoFactorVerifyResponse> result;
        if (isBackupCode)
        {
            result = await HandleBackupCode(userId, normalized, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            result = await HandleOtpCode(userId, request.Code, cancellationToken).ConfigureAwait(false);
        }

        // Track cumulative failures across all 2FA attempts
        if (result.IsFailure && (result.Error.Code == "INVALID_CODE" || result.Error.Code == "TOO_MANY_ATTEMPTS"))
        {
            user.TwoFactorFailureCount++;
            if (user.TwoFactorFailureCount >= MaxCumulativeTwoFactorFailures)
            {
                user.TwoFactorLockedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Error.Forbidden("TWO_FACTOR_LOCKED",
                    "Account is temporarily locked due to too many failed 2FA attempts. Please try again later.");
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (result.IsSuccess)
        {
            // Successful verification - reset cumulative counter
            user.TwoFactorFailureCount = 0;
            user.TwoFactorLockedAt = null;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<Result<TwoFactorVerifyResponse>> HandleOtpCode(long userId, string code, CancellationToken cancellationToken)
    {
        // Find the 2FA code by userId only (not code) so we can track failed attempts
        var twoFactorCode = await dbContext.TwoFactorCodes
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (twoFactorCode == null)
        {
            return Error.Validation("INVALID_CODE", "Invalid 2FA code");
        }

        // Check if expired
        if (twoFactorCode.ExpiresAt < DateTimeOffset.UtcNow)
        {
            dbContext.TwoFactorCodes.Remove(twoFactorCode);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Error.Validation("CODE_EXPIRED", "2FA code has expired");
        }

        // Check if code matches
        if (twoFactorCode.Code != code)
        {
            twoFactorCode.FailedAttempts++;
            if (twoFactorCode.FailedAttempts >= 5)
            {
                dbContext.TwoFactorCodes.Remove(twoFactorCode);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Error.Validation("TOO_MANY_ATTEMPTS", "Too many failed attempts. Please request a new code.");
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Error.Validation("INVALID_CODE", "Invalid 2FA code");
        }

        // Hard-delete the 2FA code
        dbContext.TwoFactorCodes.Remove(twoFactorCode);

        return await CompleteLogin(userId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<TwoFactorVerifyResponse>> HandleBackupCode(long userId, string normalizedCode, CancellationToken cancellationToken)
    {
        // Load all backup codes for this user
        var backupCodes = await dbContext.TwoFactorBackupCodes
            .Where(bc => bc.UserId == userId)
            .ToListAsync(cancellationToken);

        if (backupCodes.Count == 0)
        {
            return Error.Validation("INVALID_CODE", "Invalid 2FA code");
        }

        // Find a matching backup code by BCrypt verification - offloaded to Task.Run to avoid thread pool starvation
        Xcord.Entities.TwoFactorBackupCode? matchedCode = null;
        foreach (var bc in backupCodes)
        {
            if (await Task.Run(() => BCrypt.Net.BCrypt.Verify(normalizedCode, bc.CodeHash)))
            {
                matchedCode = bc;
                break;
            }
        }

        if (matchedCode == null)
        {
            return Error.Validation("INVALID_CODE", "Invalid 2FA code");
        }

        // Hard-delete only this specific backup code (single-use)
        dbContext.TwoFactorBackupCodes.Remove(matchedCode);

        return await CompleteLogin(userId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<TwoFactorVerifyResponse>> CompleteLogin(long userId, CancellationToken cancellationToken)
    {
        // Find user
        var user = await dbContext.Users.FindAsync(new object[] { userId }, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        // Complete login: create refresh token
        var refreshTokenValue = TokenHelper.GenerateToken();
        var refreshTokenHash = TokenHelper.HashToken(refreshTokenValue);
        var now = DateTimeOffset.UtcNow;

        var refreshToken = new Entities.RefreshToken
        {
            Id = snowflakeGenerator.NextId(),
            TokenHash = refreshTokenHash,
            UserId = userId,
            ExpiresAt = now.AddDays(30),
            CreatedAt = now
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Generate JWT access token
        var accessToken = jwtService.GenerateAccessToken(user.Id, user.IsAdmin, user.EmailConfirmed, user.IsBot);

        return new TwoFactorVerifyResponse(user.Id, user.Username, accessToken, user.EmailConfirmed, refreshTokenValue);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/2fa/verify", async (
                HttpContext httpContext,
                [FromBody] TwoFactorVerifyRequest command,
                [FromServices] TwoFactorVerifyHandler handler,
                CancellationToken ct) =>
            {
                if (handler is IValidatable<TwoFactorVerifyRequest> validatable)
                {
                    var validationError = validatable.Validate(command);
                    if (validationError is not null)
                        return Results.Problem(statusCode: validationError.StatusCode, title: validationError.Code, detail: validationError.Message);
                }

                var result = await handler.Handle(command, ct).ConfigureAwait(false);

                return result.Match(
                    success =>
                    {
                        // Set httpOnly cookies for both tokens
                        AuthCookieHelper.SetAccessTokenCookie(httpContext, success.AccessToken, 15);
                        AuthCookieHelper.SetRefreshTokenCookie(httpContext, success.RefreshToken);

                        return Results.Ok(new
                        {
                            authenticated = true,
                            userId = success.UserId,
                            username = success.Username,
                            emailConfirmed = success.EmailConfirmed
                        });
                    },
                    error => Results.Problem(
                        statusCode: error.StatusCode,
                        title: error.Code,
                        detail: error.Message)
                );
            })
            .AllowAnonymous()
            .WithName("TwoFactorVerify")
            .WithTags("Auth");
    }
}
