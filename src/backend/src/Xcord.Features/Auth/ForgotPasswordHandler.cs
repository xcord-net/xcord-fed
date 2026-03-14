using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Auth;

public sealed record ForgotPasswordRequest(string Email);

public sealed class ForgotPasswordHandler(
    AppDbContext dbContext,
    IEncryptionService encryptionService,
    SnowflakeIdGenerator snowflakeGenerator,
    ILogger<ForgotPasswordHandler> logger,
    IOutboxWriter outboxWriter,
    IOptions<InstanceOptions> instanceOptions,
    IHttpContextAccessor httpContextAccessor,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions)
    : IRequestHandler<ForgotPasswordRequest, Result<bool>>, IValidatable<ForgotPasswordRequest>
{
    private const int MaxForgotPasswordAttempts = 3;
    private static readonly TimeSpan ForgotPasswordWindow = TimeSpan.FromHours(1);

    public Error? Validate(ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Error.Validation("VALIDATION_FAILED", "Email is required");

        if (!request.Email.Contains('@'))
            return Error.Validation("VALIDATION_FAILED", "Email must be a valid email address");

        return null;
    }

    public async Task<Result<bool>> Handle(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        // ALWAYS return success (prevents user enumeration)
        // Only send email if user exists and not rate-limited

        var emailHash = encryptionService.ComputeHmac(request.Email.ToLowerInvariant());
        var emailHashHex = Convert.ToHexString(emailHash);
        var redisKey = $"{redisOptions.Value.ChannelPrefix}:forgot-pw-attempts:{emailHashHex}";

        // Increment the counter for every request (regardless of whether the user exists)
        var db = redis.GetDatabase();
        var currentCount = await db.StringIncrementAsync(redisKey);
        if (currentCount == 1)
        {
            // First request in this window - set TTL
            await db.KeyExpireAsync(redisKey, ForgotPasswordWindow);
        }

        // If over limit, skip the email send but still return 204 (no user enumeration)
        var rateLimited = currentCount > MaxForgotPasswordAttempts;

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.EmailHash == emailHash, cancellationToken);

        if (user != null && !rateLimited)
        {
            // Generate random token
            var rawToken = TokenHelper.GenerateToken();
            var tokenHash = TokenHelper.HashToken(rawToken);
            var now = DateTimeOffset.UtcNow;

            var resetToken = new PasswordResetToken
            {
                Id = snowflakeGenerator.NextId(),
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = now.AddHours(1),
                CreatedAt = now
            };

            dbContext.PasswordResetTokens.Add(resetToken);

            // Decrypt email for sending
            var decryptedEmail = encryptionService.Decrypt(user.Email);

            // Build the reset URL using the instance domain and current request scheme
            var scheme = httpContextAccessor.HttpContext?.Request.Scheme ?? "https";
            var resetUrl = BuildResetUrl(scheme, instanceOptions.Value.Domain, rawToken);
            var htmlBody = BuildResetEmailBody(user.DisplayName, resetUrl);

            // Queue password reset email via outbox
            await outboxWriter.WriteAsync(dbContext, "Email.PasswordReset", new
            {
                to = decryptedEmail,
                subject = "Reset your Xcord password",
                htmlBody
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

        }

        logger.LogInformation("Password reset requested");

        // ALWAYS return 204 regardless of whether email exists
        return true;
    }

    private static string BuildResetUrl(string scheme, string domain, string token)
    {
        var baseUrl = $"{scheme}://{domain.TrimEnd('/')}";
        return $"{baseUrl}/reset-password?token={HttpUtility.UrlEncode(token)}";
    }

    private static string BuildResetEmailBody(string displayName, string resetUrl)
    {
        return $"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            </head>
            <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background-color: #313338; margin: 0; padding: 40px 0;">
              <table width="100%" cellpadding="0" cellspacing="0" style="max-width: 480px; margin: 0 auto;">
                <tr>
                  <td style="background-color: #2b2d31; border-radius: 8px; padding: 40px;">
                    <h1 style="color: #ffffff; font-size: 22px; margin: 0 0 8px 0;">xcord</h1>
                    <h2 style="color: #dbdee1; font-size: 18px; margin: 0 0 24px 0;">Reset your password</h2>
                    <p style="color: #b5bac1; font-size: 14px; line-height: 1.6; margin: 0 0 24px 0;">
                      Hi {displayName},<br /><br />
                      We received a request to reset the password for your Xcord account.
                      Click the button below to choose a new password. This link expires in <strong style="color: #dbdee1;">1 hour</strong>.
                    </p>
                    <a href="{resetUrl}"
                       style="display: inline-block; padding: 12px 24px; background-color: #d4943a; color: #ffffff;
                              text-decoration: none; border-radius: 4px; font-size: 14px; font-weight: 600;">
                      Reset Password
                    </a>
                    <p style="color: #b5bac1; font-size: 12px; line-height: 1.6; margin: 24px 0 0 0;">
                      If you didn't request a password reset, you can safely ignore this email.
                      Your password will not be changed.
                    </p>
                    <hr style="border: none; border-top: 1px solid #3f4147; margin: 24px 0;" />
                    <p style="color: #6d6f78; font-size: 11px; margin: 0;">
                      &copy; Xcord. All rights reserved.
                    </p>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/forgot-password", async (
                [FromBody] ForgotPasswordRequest request,
                [FromServices] ForgotPasswordHandler handler,
                CancellationToken ct) =>
            await handler.ExecuteAsync(request, ct, _ => Results.NoContent()))
            .AllowAnonymous()
            .RequireRateLimiting("auth-forgot-password")
            .WithName("ForgotPassword")
            .WithTags("Auth");
    }
}
