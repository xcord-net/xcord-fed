using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Xcord.Infrastructure.Options;

namespace Xcord.Api.Authorization;

/// <summary>
/// Authorization requirement satisfied by presenting a valid X-Internal-Key
/// header matching the configured shared secret (InternalApi:Key).
/// </summary>
public sealed class InternalKeyRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Validates the X-Internal-Key header against InternalAuthOptions.Key using a
/// constant-time comparison. Replaces the previous pattern where each internal
/// endpoint was marked .AllowAnonymous() and re-implemented the header check
/// inline. Now the auth model is declarative via [Authorize(Policy = "InternalKey")].
/// </summary>
public sealed class InternalKeyAuthorizationHandler : AuthorizationHandler<InternalKeyRequirement>
{
    private const string HeaderName = "X-Internal-Key";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<InternalAuthOptions> _options;
    private readonly ILogger<InternalKeyAuthorizationHandler> _logger;

    public InternalKeyAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<InternalAuthOptions> options,
        ILogger<InternalKeyAuthorizationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        InternalKeyRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            // No HTTP context (shouldn't happen for HTTP endpoints) - fail closed.
            return Task.CompletedTask;
        }

        var providedKey = httpContext.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(providedKey))
        {
            _logger.LogWarning(
                "Internal endpoint {Path} rejected: missing {Header} header",
                httpContext.Request.Path, HeaderName);
            return Task.CompletedTask;
        }

        var configuredKey = _options.Value.Key;
        if (string.IsNullOrEmpty(configuredKey))
        {
            _logger.LogError(
                "Internal endpoint {Path} rejected: InternalApi:Key is not configured",
                httpContext.Request.Path);
            return Task.CompletedTask;
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var expectedBytes = Encoding.UTF8.GetBytes(configuredKey);
        if (CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Internal endpoint {Path} rejected: invalid {Header}",
                httpContext.Request.Path, HeaderName);
        }

        return Task.CompletedTask;
    }
}
