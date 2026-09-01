using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Xcord.Captcha;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Auth;

public sealed record RegisterRequest(
    string Username,
    string DisplayName,
    string Email,
    string Password,
    string? CaptchaId = null,
    string? CaptchaAnswer = null,
    /// <summary>
    /// Invite code the registration is scoped to. On an instance with public
    /// registration disabled, a valid code is what makes an account possible:
    /// being handed an invite by a member IS the authorisation. Without this,
    /// "join via invite from an existing member" was unreachable - the invite
    /// page sent anonymous visitors to a login they could never complete.
    /// </summary>
    string? InviteCode = null
);

public sealed partial class RegisterHandler(
    AppDbContext dbContext,
    IEncryptionService encryptionService,
    IJwtService jwtService,
    SnowflakeIdGenerator snowflakeGenerator,
    ILogger<RegisterHandler> logger,
    INotificationService notificationService,
    IOptions<EmailOptions> emailOptions,
    IOptions<TierOptions> tierOptions,
    IOptions<AuthOptions> authOptions,
    ICaptchaService captchaService)
    : IRequestHandler<RegisterRequest, Result<RegisterResponse>>, IValidatable<RegisterRequest>
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;
    private readonly TierOptions _tierOptions = tierOptions.Value;
    private readonly AuthOptions _authOptions = authOptions.Value;

    public Error? Validate(RegisterRequest request)
    {
        // Public registration off does not mean nobody may register - it means
        // registration must be vouched for. An invite code is the vouching, and
        // it is checked for real in Handle before any account is created.
        if (!_authOptions.RegistrationEnabled && string.IsNullOrWhiteSpace(request.InviteCode))
            return Error.Forbidden("REGISTRATION_DISABLED", "Public registration is disabled. Join via invite from an existing member.");

        if (string.IsNullOrWhiteSpace(request.Username))
            return Error.Validation("VALIDATION_FAILED", "Username is required");

        if (request.Username.Length > 32)
            return Error.Validation("VALIDATION_FAILED", "Username must not exceed 32 characters");

        if (!UsernameRegex().IsMatch(request.Username))
            return Error.Validation("VALIDATION_FAILED", "Username can only contain letters, numbers, underscores, and hyphens");

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return Error.Validation("VALIDATION_FAILED", "Display name is required");

        if (request.DisplayName.Length > 32)
            return Error.Validation("VALIDATION_FAILED", "Display name must not exceed 32 characters");

        if (string.IsNullOrWhiteSpace(request.Email))
            return Error.Validation("VALIDATION_FAILED", "Email is required");

        if (!request.Email.Contains('@') || request.Email.Length > 255)
            return Error.Validation("VALIDATION_FAILED", "Email must be a valid email address");

        if (string.IsNullOrWhiteSpace(request.Password))
            return Error.Validation("VALIDATION_FAILED", "Password is required");

        if (request.Password.Length < 8 || request.Password.Length > 128)
            return Error.Validation("VALIDATION_FAILED", "Password must be between 8 and 128 characters");

        return null;
    }

    public async Task<Result<RegisterResponse>> Handle(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!await captchaService.ValidateAsync(request.CaptchaId ?? "", request.CaptchaAnswer ?? ""))
            return Error.BadRequest("CAPTCHA_FAILED", "Invalid or expired captcha");

        // With public registration off, the invite is the authorisation, so it
        // has to be real before an account exists. The code is only validated
        // here, never consumed: the client follows the invite link after signing
        // up and JoinByInviteHandler consumes exactly one use, keeping max-uses
        // accounting in one place.
        if (!_authOptions.RegistrationEnabled)
        {
            var inviteError = await ValidateInviteAsync(request.InviteCode!, cancellationToken).ConfigureAwait(false);
            if (inviteError is not null) return inviteError;
        }

        // Tier gating: enforce user capacity limit (0 = unlimited)
        if (_tierOptions.MaxUsers > 0)
        {
            var activeUserCount = await dbContext.Users
                .CountAsync(u => u.DeletedAt == null, cancellationToken);

            if (activeUserCount >= _tierOptions.MaxUsers)
            {
                return Error.Forbidden("CAPACITY_EXCEEDED", "This instance has reached its maximum user capacity");
            }
        }

        // Check if username already exists
        var usernameExists = await dbContext.Users
            .AnyAsync(u => u.Username == request.Username, cancellationToken);

        if (usernameExists)
        {
            return Error.Conflict("USERNAME_TAKEN", "Username is already taken");
        }

        // Check if email already exists (by EmailHash)
        var emailHash = encryptionService.ComputeHmac(request.Email.ToLowerInvariant());
        var emailExists = await dbContext.Users
            .AnyAsync(u => u.EmailHash == emailHash, cancellationToken);

        if (emailExists)
        {
            return Error.Conflict("EMAIL_TAKEN", "Email is already registered");
        }

        // Hash password (BCrypt, configurable work factor) - offloaded to Task.Run to avoid thread pool starvation
        var passwordHash = await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(request.Password, _authOptions.BcryptWorkFactor)).ConfigureAwait(false);

        // Encrypt email
        var encryptedEmail = encryptionService.Encrypt(request.Email.ToLowerInvariant());

        // Create user
        var userId = snowflakeGenerator.NextId();
        var now = DateTimeOffset.UtcNow;

        var user = new User
        {
            Id = userId,
            Username = request.Username,
            DisplayName = request.DisplayName,
            Email = encryptedEmail,
            EmailHash = emailHash,
            PasswordHash = passwordHash,
            EmailConfirmed = false,
            TwoFactorEnabled = false,
            IsAdmin = false,
            IsBot = false,
            IsDisabled = false,
            CreatedAt = now,
            LastLoginAt = now
        };

        dbContext.Users.Add(user);

        // Generate email confirmation code (6 digits, 15 min expiry)
        var confirmationCode = GenerateConfirmationCode();
        var confirmationToken = new EmailConfirmationToken
        {
            Id = snowflakeGenerator.NextId(),
            UserId = userId,
            Code = confirmationCode,
            ExpiresAt = now.AddMinutes(15),
            CreatedAt = now
        };

        dbContext.EmailConfirmationTokens.Add(confirmationToken);

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

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Send email confirmation
        await notificationService.SendEmailAsync(
            request.Email,
            "Confirm your email address",
            $"<p>Your confirmation code is: <strong>{confirmationCode}</strong></p><p>This code expires in 15 minutes.</p>", cancellationToken);

        // Log that confirmation code was generated (code itself is not logged for security)
        logger.LogInformation("Email confirmation code generated for user {Username}", request.Username);

        // Generate JWT access token
        var accessToken = jwtService.GenerateAccessToken(userId, user.IsAdmin, user.EmailConfirmed, user.IsBot);

        return new RegisterResponse(userId, user.Username, accessToken, user.EmailConfirmed, refreshTokenValue,
            _emailOptions.DevMode ? confirmationCode : null);
    }

    private static string GenerateConfirmationCode()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }

    /// <summary>
    /// Checks that an invite code can still admit someone. Mirrors the checks in
    /// <see cref="Servers.JoinByInviteHandler"/> so a registration is never
    /// accepted against an invite the subsequent join would refuse.
    /// </summary>
    private async Task<Error?> ValidateInviteAsync(string inviteCode, CancellationToken cancellationToken)
    {
        var invite = await dbContext.Invites
            .AsNoTracking()
            .Where(i => i.Code == inviteCode)
            .Select(i => new { i.ExpiresAt, i.MaxUses, i.Uses })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (invite is null)
            return Error.NotFound("INVITE_NOT_FOUND", "Invite not found");

        if (invite.ExpiresAt.HasValue && invite.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return Error.Validation("INVITE_EXPIRED", "This invite has expired");

        if (invite.MaxUses.HasValue && invite.Uses >= invite.MaxUses.Value)
            return Error.Validation("INVITE_MAX_USES_REACHED", "This invite has reached its maximum number of uses");

        return null;
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]+$")]
    private static partial Regex UsernameRegex();

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/register", async (
                [FromBody] RegisterRequest command,
                [FromServices] RegisterHandler handler,
                HttpContext httpContext,
                CancellationToken ct) =>
            {
                if (handler is IValidatable<RegisterRequest> validatable)
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

                        var response = new Dictionary<string, object?>
                        {
                            ["authenticated"] = true,
                            ["userId"] = success.UserId,
                            ["username"] = success.Username,
                            ["emailConfirmed"] = success.EmailConfirmed
                        };
                        if (success.ConfirmationCode is not null)
                            response["confirmationCode"] = success.ConfirmationCode;

                        return Results.Ok(response);
                    },
                    error => Results.Problem(
                        statusCode: error.StatusCode,
                        title: error.Code,
                        detail: error.Message)
                );
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth-register")
            .WithName("Register")
            .WithTags("Auth");
    }
}
