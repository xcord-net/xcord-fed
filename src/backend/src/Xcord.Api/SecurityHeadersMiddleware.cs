using Microsoft.Extensions.Options;
using Xcord.Infrastructure.Options;

namespace Xcord.Api;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HubOptions _hubOptions;

    private readonly bool _isDevelopment;
    private readonly string _connectSrc;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IOptions<HubOptions> hubOptions,
        IOptions<LiveKitOptions> liveKitOptions,
        IWebHostEnvironment env)
    {
        _next = next;
        _hubOptions = hubOptions.Value;
        _isDevelopment = env.IsDevelopment();
        _connectSrc = BuildConnectSrc(liveKitOptions.Value.Host);
    }

    /// <summary>
    /// connect-src, including whatever origin voice is actually dialled on.
    /// </summary>
    /// <remarks>
    /// Joining a room is not only a WebSocket: the LiveKit client first makes an
    /// ordinary https request to the same host to validate the token. The blanket
    /// `wss:` allowance covered the socket and nothing else, so on any deployment
    /// where LiveKit is not the instance's own origin that request was refused by
    /// this very header and voice failed with "could not establish signal
    /// connection" - a failure whose cause is a policy the server itself sends.
    /// Both schemes are named because the client uses both.
    /// </remarks>
    private static string BuildConnectSrc(string? liveKitHost)
    {
        const string baseSrc = "connect-src 'self' wss: ws:";
        if (string.IsNullOrWhiteSpace(liveKitHost)) return baseSrc;
        if (!Uri.TryCreate(liveKitHost, UriKind.Absolute, out var uri)) return baseSrc;

        var httpScheme = uri.Scheme is "wss" or "https" ? "https" : "http";
        var authority = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        return $"{baseSrc} {httpScheme}://{authority}";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // CSP: frame-ancestors set based on hub linkage
            var frameAncestors = _hubOptions.Enabled && !string.IsNullOrWhiteSpace(_hubOptions.Origin)
                ? _hubOptions.Origin
                : "'none'";

            headers.Append(
                "Content-Security-Policy",
                $"default-src 'self'; " +
                $"script-src 'self'; " +
                $"{_connectSrc}; " +
                $"img-src 'self' blob:; " +
                $"style-src 'self'; " +
                $"media-src 'self' blob:; " +
                $"font-src 'self'; " +
                $"frame-ancestors {frameAncestors}");

            // X-Content-Type-Options
            headers.Append(
                "X-Content-Type-Options",
                "nosniff");

            // X-Frame-Options (for older browsers)
            headers.Append(
                "X-Frame-Options",
                frameAncestors == "'none'" ? "DENY" : "SAMEORIGIN");

            // HSTS (only in non-development to avoid poisoning local browser caches)
            if (!_isDevelopment)
            {
                headers.Append(
                    "Strict-Transport-Security",
                    "max-age=31536000; includeSubDomains");
            }

            // Referrer-Policy
            headers.Append(
                "Referrer-Policy",
                "strict-origin-when-cross-origin");

            // Permissions-Policy
            headers.Append(
                "Permissions-Policy",
                "camera=(), microphone=(self), geolocation=(), payment=()");

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
