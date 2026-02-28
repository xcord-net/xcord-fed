using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.RegularExpressions;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

using Xcord.Infrastructure.Data;

namespace Xcord.Features.Auth;

public sealed record ConfirmEmailRequest(
    long UserId,
    string Code
);

public sealed partial class ConfirmEmailHandler(
    AppDbContext dbContext,
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions)
    : IRequestHandler<ConfirmEmailRequest, Result<bool>>, IValidatable<ConfirmEmailRequest>
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    public Error? Validate(ConfirmEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Error.Validation("VALIDATION_FAILED", "Code is required");

        if (request.Code.Length != 6 || !CodeRegex().IsMatch(request.Code))
            return Error.Validation("VALIDATION_FAILED", "Code must be a 6-digit number");

        return null;
    }

    public async Task<Result<bool>> Handle(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        // Rate-limit confirmation attempts per user
        var attemptKey = $"{redisOptions.Value.ChannelPrefix}:email-confirm-attempts:{request.UserId}";
        var db = redis.GetDatabase();
        var currentCount = (long?)await db.StringGetAsync(attemptKey);
        if (currentCount >= MaxAttempts)
        {
            var ttl = await db.KeyTimeToLiveAsync(attemptKey);
            var retryAfter = ttl.HasValue ? (int)Math.Ceiling(ttl.Value.TotalSeconds) : (int)AttemptWindow.TotalSeconds;
            return Error.RateLimited("CONFIRM_EMAIL_RATE_LIMITED", retryAfter.ToString());
        }

        // Find the confirmation token
        var token = await dbContext.EmailConfirmationTokens
            .FirstOrDefaultAsync(t => t.UserId == request.UserId && t.Code == request.Code, cancellationToken);

        if (token == null)
        {
            // Increment attempt counter on failure
            var count = await db.StringIncrementAsync(attemptKey);
            if (count == 1) await db.KeyExpireAsync(attemptKey, AttemptWindow);
            return Error.Validation("INVALID_CODE", "Invalid confirmation code");
        }

        // Check if expired
        if (token.ExpiresAt < DateTimeOffset.UtcNow)
        {
            dbContext.EmailConfirmationTokens.Remove(token);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Error.Validation("CODE_EXPIRED", "Confirmation code has expired");
        }

        // Update user email confirmed status
        var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        if (user == null)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        user.EmailConfirmed = true;

        // Hard-delete the token
        dbContext.EmailConfirmationTokens.Remove(token);

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    [GeneratedRegex("^[0-9]{6}$")]
    private static partial Regex CodeRegex();

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/confirm-email", async (
                string code,
                [FromServices] ICurrentUserService currentUserService,
                [FromServices] ConfirmEmailHandler handler,
                CancellationToken ct) =>
            {
                var userIdResult = currentUserService.GetCurrentUserId();
                if (userIdResult.IsFailure)
                    return Results.Problem(statusCode: userIdResult.Error.StatusCode, title: userIdResult.Error.Code, detail: userIdResult.Error.Message);
                var userId = userIdResult.Value;

                var command = new ConfirmEmailRequest(userId, code);
                return await handler.ExecuteAsync(command, ct, success => Results.Ok(new { confirmed = success }));
            })
            .RequireAuthorization()
            .WithName("ConfirmEmail")
            .WithTags("Auth");
    }
}
