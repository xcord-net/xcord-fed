using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using Xcord.Infrastructure.Services;

using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Auth;

public sealed record TwoFactorEnableRequest(long UserId);

public sealed class TwoFactorEnableHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IEncryptionService encryptionService,
    IOutboxWriter outboxWriter,
    ILogger<TwoFactorEnableHandler> logger)
    : IRequestHandler<TwoFactorEnableRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(TwoFactorEnableRequest request, CancellationToken cancellationToken)
    {
        // Find user
        var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        if (user == null)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        // Check if email is confirmed (required for 2FA)
        if (!user.EmailConfirmed)
        {
            return Error.Validation("EMAIL_NOT_CONFIRMED", "Email must be confirmed before enabling 2FA");
        }

        // Check if 2FA is already enabled
        if (user.TwoFactorEnabled)
        {
            return Error.Validation("ALREADY_ENABLED", "Two-factor authentication is already enabled");
        }

        // Generate 6-digit code (10 min expiry)
        var code = GenerateTwoFactorCode();
        var now = DateTimeOffset.UtcNow;

        var twoFactorCode = new TwoFactorCode
        {
            Id = snowflakeGenerator.NextId(),
            UserId = request.UserId,
            Code = code,
            ExpiresAt = now.AddMinutes(10),
            CreatedAt = now
        };

        dbContext.TwoFactorCodes.Add(twoFactorCode);

        // Decrypt user email to send the code
        var plaintextEmail = encryptionService.Decrypt(user.Email);

        await outboxWriter.WriteAsync(dbContext, "Email.TwoFactorEnable", new
        {
            to = plaintextEmail,
            subject = "Your two-factor authentication setup code",
            htmlBody = $"<p>Your two-factor authentication setup code is: <strong>{code}</strong></p><p>This code expires in 10 minutes. If you did not request this, you can ignore this message.</p>"
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("2FA enable code sent for user {UserId}", request.UserId);

        return true;
    }

    private static string GenerateTwoFactorCode()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/2fa/enable", async (
                [FromServices] ICurrentUserService currentUserService,
                [FromServices] TwoFactorEnableHandler handler,
                CancellationToken ct) =>
            {
                var userIdResult = currentUserService.GetCurrentUserId();
                if (userIdResult.IsFailure)
                    return Results.Problem(statusCode: userIdResult.Error.StatusCode, title: userIdResult.Error.Code, detail: userIdResult.Error.Message);
                var userId = userIdResult.Value;

                var command = new TwoFactorEnableRequest(userId);
                return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
            })
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("TwoFactorEnable")
            .WithTags("Auth");
    }
}
