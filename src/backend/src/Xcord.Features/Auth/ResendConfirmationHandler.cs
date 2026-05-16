using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Auth;

public sealed record ResendConfirmationRequest(long UserId);

public sealed class ResendConfirmationHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ILogger<ResendConfirmationHandler> logger,
    IEncryptionService encryptionService,
    INotificationService notificationService)
    : IRequestHandler<ResendConfirmationRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        // Find user
        var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        // Check if email already confirmed
        if (user.EmailConfirmed)
        {
            return Error.Validation("ALREADY_CONFIRMED", "Email is already confirmed");
        }

        // Rate limit: check if a code was created in the last 60 seconds
        var recentCode = await dbContext.EmailConfirmationTokens
            .Where(t => t.UserId == request.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (recentCode != null && recentCode.CreatedAt.AddSeconds(60) > DateTimeOffset.UtcNow)
        {
            return Error.Validation("RATE_LIMIT", "Please wait before requesting another confirmation code");
        }

        // Generate new 6-digit code (15 min expiry)
        var confirmationCode = GenerateConfirmationCode();
        var now = DateTimeOffset.UtcNow;

        var confirmationToken = new EmailConfirmationToken
        {
            Id = snowflakeGenerator.NextId(),
            UserId = request.UserId,
            Code = confirmationCode,
            ExpiresAt = now.AddMinutes(15),
            CreatedAt = now
        };

        dbContext.EmailConfirmationTokens.Add(confirmationToken);

        // Decrypt email for sending
        var decryptedEmail = encryptionService.Decrypt(user.Email);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Send email confirmation
        await notificationService.SendEmailAsync(
            decryptedEmail,
            "Confirm your email address",
            $"<p>Your confirmation code is: <strong>{confirmationCode}</strong></p><p>This code expires in 15 minutes.</p>", cancellationToken);

        logger.LogInformation("Email confirmation code sent for user {UserId}", request.UserId);

        return true;
    }

    private static string GenerateConfirmationCode()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/resend-confirmation", async (
                [FromServices] ICurrentUserService currentUserService,
                [FromServices] ResendConfirmationHandler handler,
                CancellationToken ct) =>
            {
                var userIdResult = currentUserService.GetCurrentUserId();
                if (userIdResult.IsFailure)
                    return Results.Problem(statusCode: userIdResult.Error.StatusCode, title: userIdResult.Error.Code, detail: userIdResult.Error.Message);
                var userId = userIdResult.Value;

                var command = new ResendConfirmationRequest(userId);
                return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
            })
            .RequireAuthorization()
            .WithName("ResendConfirmation")
            .WithTags("Auth");
    }
}
