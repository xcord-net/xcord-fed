using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Auth;

public static class AuthCookieHelper
{
    public static void SetAccessTokenCookie(HttpContext httpContext, string accessToken, int expirationMinutes)
    {
        var (sameSite, secure) = GetCookiePolicy(httpContext);

        httpContext.Response.Cookies.Append("access_token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSite,
            Expires = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes),
            Path = "/"
        });
    }

    public static void SetRefreshTokenCookie(HttpContext httpContext, string refreshToken)
    {
        var (sameSite, secure) = GetCookiePolicy(httpContext);

        httpContext.Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSite,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/"
        });
    }

    public static void DeleteAuthCookies(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete("access_token", new CookieOptions { Path = "/" });
        httpContext.Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/" });
    }

    private static (SameSiteMode SameSite, bool Secure) GetCookiePolicy(HttpContext httpContext)
    {
        var origin = httpContext.Request.Headers.Origin.FirstOrDefault() ?? "";
        var isMobile = origin.StartsWith("capacitor://") || origin == "https://localhost";

        if (isMobile)
            return (SameSiteMode.None, true);

        return (SameSiteMode.Strict, httpContext.Request.IsHttps);
    }
}
