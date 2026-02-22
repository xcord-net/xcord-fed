using Microsoft.Extensions.Options;
using Xcord.Infrastructure.Options;

namespace Xcord.Api;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HubOptions _hubOptions;

    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<HubOptions> hubOptions, IWebHostEnvironment env)
    {
        _next = next;
        _hubOptions = hubOptions.Value;
        _isDevelopment = env.IsDevelopment();
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
                $"connect-src 'self' wss: ws:; " +
                $"img-src 'self' blob: data:; " +
                $"style-src 'self' 'unsafe-inline'; " +
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
