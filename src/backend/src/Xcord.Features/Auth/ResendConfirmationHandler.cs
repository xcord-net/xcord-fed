using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
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
    IOutboxWriter outboxWriter)
    : IRequestHandler<ResendConfirmationRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        // Find user
        var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
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

        // Queue email confirmation via outbox
        await outboxWriter.WriteAsync(dbContext, "Email.Confirmation", new
        {
            to = decryptedEmail,
            subject = "Confirm your email address",
            htmlBody = $"<p>Your confirmation code is: <strong>{confirmationCode}</strong></p><p>This code expires in 15 minutes.</p>"
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

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
                HttpContext httpContext,
                [FromServices] ResendConfirmationHandler handler,
                CancellationToken ct) =>
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null || !long.TryParse(userIdClaim, out var userId))
                {
                    return Results.Unauthorized();
                }

                var command = new ResendConfirmationRequest(userId);
                return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
            })
            .RequireAuthorization()
            .WithName("ResendConfirmation")
            .WithTags("Auth");
    }
}
