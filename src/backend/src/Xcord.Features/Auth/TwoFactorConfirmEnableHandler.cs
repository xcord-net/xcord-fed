using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

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

public sealed class TwoFactorConfirmEnableHandler(AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator, IOptions<AuthOptions> authOptions)
    : IRequestHandler<TwoFactorConfirmEnableRequest, Result<TwoFactorConfirmEnableResponse>>, IValidatable<TwoFactorConfirmEnableRequest>
{
    private readonly AuthOptions _authOptions = authOptions.Value;
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
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Error.Validation("CODE_EXPIRED", "2FA code has expired");
        }

        // Find user
        var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken).ConfigureAwait(false);
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

        // Generate 10 new backup codes.
        //
        // Hashed together rather than one after another. They are independent
        // values, so on a host with cores to spare this is much faster than a
        // sequential loop. On an instance held to its tier's CPU quota it is
        // not: measured there, one hash costs ~280ms and ten still cost ~2.4s,
        // because there is no spare parallelism to use. Kept anyway - it is
        // free where the capacity exists - but the endpoint is slow by nature
        // and is budgeted as such rather than pretended otherwise.
        var now = DateTimeOffset.UtcNow;
        var rawCodes = new string[BackupCodeCount];
        var plaintextCodes = new List<string>(BackupCodeCount);

        for (int i = 0; i < BackupCodeCount; i++)
        {
            rawCodes[i] = GenerateBackupCode();
            plaintextCodes.Add($"{rawCodes[i][..4]}-{rawCodes[i][4..]}");
        }

        // Hashed without the hyphen, on the thread pool so the request thread is
        // not held for the duration.
        var hashes = await Task.WhenAll(rawCodes.Select(raw =>
            Task.Run(() => BCrypt.Net.BCrypt.HashPassword(raw, _authOptions.BcryptWorkFactor), cancellationToken)))
            .ConfigureAwait(false);

        foreach (var codeHash in hashes)
        {
            dbContext.TwoFactorBackupCodes.Add(new TwoFactorBackupCode
            {
                Id = snowflakeGenerator.NextId(),
                UserId = request.UserId,
                CodeHash = codeHash,
                CreatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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

    public sealed record TwoFactorConfirmEnableBody(string Code);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/2fa/confirm-enable", async (
                [FromBody] TwoFactorConfirmEnableBody body,
                [FromServices] ICurrentUserService currentUserService,
                [FromServices] TwoFactorConfirmEnableHandler handler,
                CancellationToken ct) =>
            {
                var userIdResult = currentUserService.GetCurrentUserId();
                if (userIdResult.IsFailure)
                    return Results.Problem(statusCode: userIdResult.Error.StatusCode, title: userIdResult.Error.Code, detail: userIdResult.Error.Message);
                var userId = userIdResult.Value;

                var command = new TwoFactorConfirmEnableRequest(userId, body.Code);
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
