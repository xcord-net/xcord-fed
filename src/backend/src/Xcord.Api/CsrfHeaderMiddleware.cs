namespace Xcord.Api;

/// <summary>
/// CSRF defense via custom request header.
///
/// Browsers do not send custom (non-CORS-safelisted) request headers on cross-origin
/// form submissions, image loads, link prefetches, or simple navigations. By requiring
/// a custom header on cookie-authenticated state-changing requests, we prevent attacker
/// pages from forging requests against a victim's session even when the auth cookie is
/// attached automatically by the browser.
///
/// Rules:
///  - Safe methods (GET, HEAD, OPTIONS) are always allowed.
///  - Requests without an auth cookie (access_token / refresh_token) are allowed: they
///    cannot be CSRF'd because there is no ambient credential. Bot tokens, webhook
///    signatures, and Bearer-only API clients fall under this branch.
///  - Cookie-authenticated POST/PUT/PATCH/DELETE must include a non-empty
///    X-Xcord-Request header. Missing or empty header returns 403.
/// </summary>
public sealed class CsrfHeaderMiddleware
{
    public const string HeaderName = "X-Xcord-Request";
    private const string AccessTokenCookie = "access_token";
    private const string RefreshTokenCookie = "refresh_token";

    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS"
    };

    private readonly RequestDelegate _next;

    public CsrfHeaderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        if (SafeMethods.Contains(method))
        {
            await _next(context);
            return;
        }

        // SignalR negotiate (/hubs/*) authenticates via a one-time ticket in the query
        // string, not cookies. The browser may attach the auth cookie incidentally on
        // same-origin requests, but the hub does not trust it. Skip CSRF enforcement on
        // these paths so real-time connections are not blocked.
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            await _next(context);
            return;
        }

        var hasCookieAuth =
            context.Request.Cookies.ContainsKey(AccessTokenCookie) ||
            context.Request.Cookies.ContainsKey(RefreshTokenCookie);

        if (!hasCookieAuth)
        {
            await _next(context);
            return;
        }

        var headerValue = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(headerValue))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"error\":\"CSRF_HEADER_MISSING\"," +
                "\"message\":\"State-changing requests with cookie auth must include the " +
                HeaderName + " header.\"}");
            return;
        }

        await _next(context);
    }
}

public static class CsrfHeaderMiddlewareExtensions
{
    public static IApplicationBuilder UseCsrfHeader(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CsrfHeaderMiddleware>();
    }
}
