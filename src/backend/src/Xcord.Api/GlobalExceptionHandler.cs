using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Xcord.Api;

/// <summary>
/// Unhandled-exception fallback. Emits an RFC 7807 ProblemDetails body so that
/// every API failure response shares a single shape with the rest of the
/// handlers (which return Results.Problem(...)).
///
/// Logs are structured with the exception type, message, correlation/trace id,
/// and the request method + path so that production triage is possible.
/// Stack traces are written at Debug in production and Error in Development so
/// that local debugging still surfaces the full stack at the default log level.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        var exceptionType = exception.GetType().Name;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["RequestMethod"] = httpContext.Request.Method,
            ["RequestPath"] = httpContext.Request.Path.Value,
        }))
        {
            _logger.LogError(
                "Unhandled exception {ExceptionType}: {Message}",
                exceptionType, exception.Message);

            // Stack trace at Debug in production keeps logs compact; in dev we
            // emit it at Error so local stack traces are visible at default verbosity.
            if (_environment.IsDevelopment())
            {
                _logger.LogError(exception, "Stack trace for {ExceptionType}", exceptionType);
            }
            else
            {
                _logger.LogDebug(exception, "Stack trace for {ExceptionType}", exceptionType);
            }
        }

        var problem = new ProblemDetails
        {
            Status = (int)HttpStatusCode.InternalServerError,
            Title = "internal_error",
            Detail = "An unexpected error occurred",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        };
        problem.Extensions["traceId"] = traceId;

        // In Development we include the exception text so devs can copy/paste
        // straight from the response body. We deliberately do NOT leak this in
        // production builds.
        if (_environment.IsDevelopment())
        {
            problem.Extensions["exception"] = exception.ToString();
        }

        httpContext.Response.StatusCode = problem.Status.Value;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
