using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        var tokenHash = TokenHelper.HashToken(refreshTokenValue);

        var refreshToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken != null)
        {
            dbContext.RefreshTokens.Remove(refreshToken);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
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
                    await handler.HandleWithToken(refreshTokenValue, ct).ConfigureAwait(false);
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
