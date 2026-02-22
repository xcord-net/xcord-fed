using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Auth;

public static class AuthCookieHelper
{
    public static void SetAccessTokenCookie(HttpContext httpContext, string accessToken, int expirationMinutes)
    {
        httpContext.Response.Cookies.Append("access_token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes),
            Path = "/"
        });
    }

    public static void SetRefreshTokenCookie(HttpContext httpContext, string refreshToken)
    {
        httpContext.Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/"
        });
    }

    public static void DeleteAuthCookies(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete("access_token", new CookieOptions { Path = "/" });
        httpContext.Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/" });
    }
}
