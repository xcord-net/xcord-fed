using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Auth;

public sealed record TwoFactorConfirmEnableRequest(
    long UserId,
    string Code
);

public sealed record TwoFactorConfirmEnableResponse(
    bool Enabled,
    IReadOnlyList<string> BackupCodes
);

public sealed class TwoFactorConfirmEnableHandler(AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator)
    : IRequestHandler<TwoFactorConfirmEnableRequest, Result<TwoFactorConfirmEnableResponse>>, IValidatable<TwoFactorConfirmEnableRequest>
{
    private const int BackupCodeCount = 10;
    private const string BackupCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public Error? Validate(TwoFactorConfirmEnableRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Error.Validation("VALIDATION_FAILED", "Code is required");

        if (request.Code.Length != 6)
            return Error.Validation("VALIDATION_FAILED", "Code must be 6 digits");

        return null;
    }

    public async Task<Result<TwoFactorConfirmEnableResponse>> Handle(TwoFactorConfirmEnableRequest request, CancellationToken cancellationToken)
    {
        // Find the 2FA code
        var twoFactorCode = await dbContext.TwoFactorCodes
            .FirstOrDefaultAsync(t => t.UserId == request.UserId && t.Code == request.Code, cancellationToken);

        if (twoFactorCode == null)
        {
            return Error.Validation("INVALID_CODE", "Invalid 2FA code");
        }

        // Check if expired
        if (twoFactorCode.ExpiresAt < DateTimeOffset.UtcNow)
        {
            dbContext.TwoFactorCodes.Remove(twoFactorCode);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Error.Validation("CODE_EXPIRED", "2FA code has expired");
        }

        // Find user
        var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        if (user == null)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        // Enable 2FA
        user.TwoFactorEnabled = true;

        // Hard-delete the verification code
        dbContext.TwoFactorCodes.Remove(twoFactorCode);

        // Delete any existing backup codes for this user (re-enable scenario)
        var existingBackupCodes = await dbContext.TwoFactorBackupCodes
            .Where(bc => bc.UserId == request.UserId)
            .ToListAsync(cancellationToken);
        dbContext.TwoFactorBackupCodes.RemoveRange(existingBackupCodes);

        // Generate 10 new backup codes
        var now = DateTimeOffset.UtcNow;
        var plaintextCodes = new List<string>(BackupCodeCount);

        for (int i = 0; i < BackupCodeCount; i++)
        {
            var rawCode = GenerateBackupCode();
            var formatted = $"{rawCode[..4]}-{rawCode[4..]}";
            plaintextCodes.Add(formatted);

            // Hash without hyphen
            var codeHash = BCrypt.Net.BCrypt.HashPassword(rawCode);

            dbContext.TwoFactorBackupCodes.Add(new TwoFactorBackupCode
            {
                Id = snowflakeGenerator.NextId(),
                UserId = request.UserId,
                CodeHash = codeHash,
                CreatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new TwoFactorConfirmEnableResponse(true, plaintextCodes);
    }

    /// <summary>
    /// Generates an 8-character alphanumeric backup code using a crypto-safe RNG.
    /// Uses an unambiguous alphabet (no O/0/I/1/l) for readability.
    /// </summary>
    private static string GenerateBackupCode()
    {
        var result = new StringBuilder(8);
        var randomBytes = new byte[8];
        using var rng = RandomNumberGenerator.Create();

        // Generate bytes and map to alphabet characters
        rng.GetBytes(randomBytes);
        foreach (var b in randomBytes)
        {
            result.Append(BackupCodeAlphabet[b % BackupCodeAlphabet.Length]);
        }

        return result.ToString();
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/2fa/confirm-enable", async (
                HttpContext httpContext,
                string code,
                [FromServices] TwoFactorConfirmEnableHandler handler,
                CancellationToken ct) =>
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null || !long.TryParse(userIdClaim, out var userId))
                {
                    return Results.Unauthorized();
                }

                var command = new TwoFactorConfirmEnableRequest(userId, code);
                return await handler.ExecuteAsync(command, ct, success => Results.Ok(new
                {
                    enabled = success.Enabled,
                    backupCodes = success.BackupCodes
                }));
            })
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("TwoFactorConfirmEnable")
            .WithTags("Auth");
    }
}
