using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Auth;

public sealed record RefreshTokenRequest;

public sealed record RefreshTokenResponse(string AccessToken, string RefreshToken);

public sealed class RefreshTokenHandler(
    AppDbContext dbContext,
    IJwtService jwtService,
    SnowflakeIdGenerator snowflakeGenerator)
    : IRequestHandler<RefreshTokenRequest, Result<RefreshTokenResponse>>
{
    public Task<Result<RefreshTokenResponse>> Handle(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        // Note: The refresh token value will be passed from the endpoint via context
        return Task.FromResult<Result<RefreshTokenResponse>>(Error.Validation("INVALID_TOKEN", "Invalid or expired refresh token"));
    }

    public async Task<Result<RefreshTokenResponse>> HandleWithToken(string refreshTokenValue, CancellationToken cancellationToken)
    {
        // Hash the token
        var tokenHash = TokenHelper.HashToken(refreshTokenValue);

        // Find the refresh token in database
        var refreshToken = await dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken == null)
        {
            return Error.Validation("INVALID_TOKEN", "Invalid or expired refresh token");
        }

        // Check if expired
        if (refreshToken.ExpiresAt < DateTimeOffset.UtcNow)
        {
            dbContext.RefreshTokens.Remove(refreshToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Error.Validation("INVALID_TOKEN", "Invalid or expired refresh token");
        }

        // Check if user account is disabled
        if (refreshToken.User.IsDisabled)
        {
            return Error.Forbidden("ACCOUNT_DISABLED", "Account is disabled");
        }

        // Hard-delete old refresh token (rotation)
        dbContext.RefreshTokens.Remove(refreshToken);

        // Create new refresh token (30 days)
        var newRefreshTokenValue = TokenHelper.GenerateRefreshToken();
        var newRefreshTokenHash = TokenHelper.HashToken(newRefreshTokenValue);
        var now = DateTimeOffset.UtcNow;

        var newRefreshToken = new Entities.RefreshToken
        {
            Id = snowflakeGenerator.NextId(),
            TokenHash = newRefreshTokenHash,
            UserId = refreshToken.UserId,
            ExpiresAt = now.AddDays(30),
            CreatedAt = now
        };

        dbContext.RefreshTokens.Add(newRefreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Generate new JWT access token
        var accessToken = jwtService.GenerateAccessToken(
            refreshToken.User.Id,
            refreshToken.User.IsAdmin,
            refreshToken.User.EmailConfirmed,
            refreshToken.User.IsBot);

        return new RefreshTokenResponse(accessToken, newRefreshTokenValue);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/refresh", async (
                HttpContext httpContext,
                [FromServices] RefreshTokenHandler handler,
                CancellationToken ct) =>
            {
                // Read refresh token from httpOnly cookie
                if (!httpContext.Request.Cookies.TryGetValue("refresh_token", out var refreshTokenValue) ||
                    string.IsNullOrWhiteSpace(refreshTokenValue))
                {
                    return Results.Problem(
                        statusCode: 400,
                        title: "MISSING_TOKEN",
                        detail: "Refresh token not found");
                }

                var result = await handler.HandleWithToken(refreshTokenValue, ct);

                return result.Match(
                    success =>
                    {
                        // Set httpOnly cookies for both tokens
                        AuthCookieHelper.SetAccessTokenCookie(httpContext, success.AccessToken, 15);
                        AuthCookieHelper.SetRefreshTokenCookie(httpContext, success.RefreshToken);

                        return Results.Ok(new { authenticated = true });
                    },
                    error => Results.Problem(
                        statusCode: error.StatusCode,
                        title: error.Code,
                        detail: error.Message)
                );
            })
            .AllowAnonymous()
            .WithName("RefreshToken")
            .WithTags("Auth");
    }
}
