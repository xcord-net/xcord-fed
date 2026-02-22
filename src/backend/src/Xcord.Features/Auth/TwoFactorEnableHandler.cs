using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Security.Cryptography;

using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Auth;

public sealed record TwoFactorEnableRequest(long UserId);

public sealed class TwoFactorEnableHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
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
                HttpContext httpContext,
                [FromServices] TwoFactorEnableHandler handler,
                CancellationToken ct) =>
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null || !long.TryParse(userIdClaim, out var userId))
                {
                    return Results.Unauthorized();
                }

                var command = new TwoFactorEnableRequest(userId);
                return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
            })
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("TwoFactorEnable")
            .WithTags("Auth");
    }
}
