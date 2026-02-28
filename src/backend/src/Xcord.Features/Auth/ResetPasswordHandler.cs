using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Xcord.Infrastructure.Options;

using Xcord.Infrastructure.Data;

namespace Xcord.Features.Auth;

public sealed record ResetPasswordRequest(
    string Token,
    string NewPassword
);

public sealed class ResetPasswordHandler(AppDbContext dbContext, IOptions<AuthOptions> authOptions)
    : IRequestHandler<ResetPasswordRequest, Result<bool>>, IValidatable<ResetPasswordRequest>
{
    private readonly AuthOptions _authOptions = authOptions.Value;

    public Error? Validate(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Error.Validation("VALIDATION_FAILED", "Token is required");

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return Error.Validation("VALIDATION_FAILED", "Password is required");

        if (request.NewPassword.Length < 8)
            return Error.Validation("VALIDATION_FAILED", "Password must be at least 8 characters");

        return null;
    }

    public async Task<Result<bool>> Handle(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        // Hash the token
        var tokenHash = HashToken(request.Token);

        // Find the reset token
        var resetToken = await dbContext.PasswordResetTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (resetToken == null)
        {
            return Error.Validation("INVALID_TOKEN", "Invalid or expired reset token");
        }

        // Check if expired
        if (resetToken.ExpiresAt < DateTimeOffset.UtcNow)
        {
            dbContext.PasswordResetTokens.Remove(resetToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Error.Validation("INVALID_TOKEN", "Invalid or expired reset token");
        }

        // Hash new password (BCrypt, configurable work factor) — offloaded to Task.Run to avoid thread pool starvation
        resetToken.User.PasswordHash = await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(request.NewPassword, _authOptions.BcryptWorkFactor));

        // Delete ALL refresh tokens for the user (force re-login everywhere)
        var refreshTokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == resetToken.UserId)
            .ToListAsync(cancellationToken);

        dbContext.RefreshTokens.RemoveRange(refreshTokens);

        // Hard-delete the reset token
        dbContext.PasswordResetTokens.Remove(resetToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/reset-password", async (
                [FromBody] ResetPasswordRequest request,
                [FromServices] ResetPasswordHandler handler,
                CancellationToken ct) =>
            await handler.ExecuteAsync(request, ct, _ => Results.NoContent()))
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithName("ResetPassword")
            .WithTags("Auth");
    }
}
