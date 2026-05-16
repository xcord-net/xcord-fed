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

        // SignalR routes (/hubs/*) require special handling. The cookie -> Authorization
        // header middleware in Program.cs means a SignalR negotiate POST CAN authenticate
        // via the auth cookie alone if the browser attaches it. That makes negotiate a
        // CSRF-relevant entry point in principle: a cross-origin attacker could try to
        // establish a hub connection on behalf of a victim. To prevent that we require
        // EITHER the X-Xcord-Request custom header OR a one-time ?ticket= query string
        // on the negotiate request. The ticket is minted by /api/v1/auth/ws-ticket --
        // an endpoint that already enforces the X-Xcord-Request CSRF header -- so its
        // presence on the negotiate URL transitively proves CSRF safety.
        //
        // SignalR transport frames (websocket upgrade, long-polling /hubs/main?id=...)
        // never authenticate via cookies (the client passes the token in the access_token
        // query string, see OnMessageReceived in ServiceCollectionExtensions), so they
        // skip CSRF entirely.
        var path = context.Request.Path;
        if (path.StartsWithSegments("/hubs"))
        {
            var isNegotiate = path.Value!.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase);
            if (!isNegotiate)
            {
                await _next(context);
                return;
            }

            // Negotiate: a valid ticket query string substitutes for the CSRF header.
            // The TicketAuthHandler later validates and consumes the ticket; here we
            // only check that one is present so the SignalR JS client (which cannot
            // attach custom headers on its internal negotiate POST in all browsers)
            // is not blocked outright.
            if (!string.IsNullOrEmpty(context.Request.Query["ticket"].ToString()))
            {
                await _next(context);
                return;
            }
            // Fall through to the standard cookie-auth + X-Xcord-Request check.
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
