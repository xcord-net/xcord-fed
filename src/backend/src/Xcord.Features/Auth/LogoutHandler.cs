using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Auth;

public sealed record LogoutRequest;

public sealed class LogoutHandler(AppDbContext dbContext)
    : IRequestHandler<LogoutRequest, Result<bool>>
{
    public Task<Result<bool>> Handle(LogoutRequest request, CancellationToken cancellationToken)
    {
        // Note: The refresh token value will be passed from the endpoint via context
        return Task.FromResult<Result<bool>>(true);
    }

    public async Task<Result<bool>> HandleWithToken(string refreshTokenValue, CancellationToken cancellationToken)
    {
        // Hash the token
        var tokenHash = HashToken(refreshTokenValue);

        // Find and delete the refresh token
        var refreshToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken != null)
        {
            dbContext.RefreshTokens.Remove(refreshToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

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
        return app.MapPost("/api/v1/auth/logout", async (
                HttpContext httpContext,
                CancellationToken ct) =>
            {
                // Try to get and delete refresh token from cookie
                if (httpContext.Request.Cookies.TryGetValue("refresh_token", out var refreshTokenValue) &&
                    !string.IsNullOrWhiteSpace(refreshTokenValue))
                {
                    var handler = httpContext.RequestServices.GetRequiredService<LogoutHandler>();
                    await handler.HandleWithToken(refreshTokenValue, ct);
                }

                // Clear both cookies regardless
                AuthCookieHelper.DeleteAuthCookies(httpContext);

                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("Logout")
            .WithTags("Auth");
    }
}
