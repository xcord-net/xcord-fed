using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Auth;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ChangePasswordInternalRequest(
    long UserId,
    string CurrentPassword,
    string NewPassword
);

public sealed class ChangePasswordHandler(AppDbContext dbContext, IOptions<AuthOptions> authOptions)
    : IRequestHandler<ChangePasswordInternalRequest, Result<bool>>, IValidatable<ChangePasswordInternalRequest>
{
    private readonly AuthOptions _authOptions = authOptions.Value;

    public Error? Validate(ChangePasswordInternalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return Error.Validation("VALIDATION_FAILED", "Current password is required");

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return Error.Validation("VALIDATION_FAILED", "New password is required");

        if (request.NewPassword.Length < 8)
            return Error.Validation("VALIDATION_FAILED", "New password must be at least 8 characters");

        return null;
    }

    public async Task<Result<bool>> Handle(ChangePasswordInternalRequest request, CancellationToken cancellationToken)
    {
        // Find user
        var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        if (user == null)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        // Verify current password - offloaded to Task.Run to avoid thread pool starvation
        if (!await Task.Run(() => BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash)))
        {
            return Error.Validation("INVALID_PASSWORD", "Current password is incorrect");
        }

        // Hash new password (BCrypt, configurable work factor) - offloaded to Task.Run to avoid thread pool starvation
        user.PasswordHash = await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(request.NewPassword, _authOptions.BcryptWorkFactor));

        // Delete ALL refresh tokens for the user (force re-login everywhere)
        var refreshTokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == request.UserId)
            .ToListAsync(cancellationToken);

        dbContext.RefreshTokens.RemoveRange(refreshTokens);

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/change-password", async (
                HttpContext httpContext,
                [FromBody] ChangePasswordRequest request,
                [FromServices] ICurrentUserService currentUserService,
                [FromServices] ChangePasswordHandler handler,
                CancellationToken ct) =>
            {
                var userIdResult = currentUserService.GetCurrentUserId();
                if (userIdResult.IsFailure)
                    return Results.Problem(statusCode: userIdResult.Error.StatusCode, title: userIdResult.Error.Code, detail: userIdResult.Error.Message);
                var userId = userIdResult.Value;

                var command = new ChangePasswordInternalRequest(userId, request.CurrentPassword, request.NewPassword);

                if (handler is IValidatable<ChangePasswordInternalRequest> validatable)
                {
                    var validationError = validatable.Validate(command);
                    if (validationError is not null)
                        return Results.Problem(statusCode: validationError.StatusCode, title: validationError.Code, detail: validationError.Message);
                }

                var result = await handler.Handle(command, ct);

                return result.Match(
                    success =>
                    {
                        // Clear the httpOnly cookie (user must re-login)
                        httpContext.Response.Cookies.Delete("refresh_token");
                        return Results.NoContent();
                    },
                    error => Results.Problem(
                        statusCode: error.StatusCode,
                        title: error.Code,
                        detail: error.Message)
                );
            })
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("ChangePassword")
            .WithTags("Auth");
    }
}
