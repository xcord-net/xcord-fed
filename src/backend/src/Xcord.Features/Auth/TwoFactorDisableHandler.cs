using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Services;

using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Auth;

public sealed record TwoFactorDisableRequest(string CurrentPassword);

public sealed record TwoFactorDisableInternalRequest(
    long UserId,
    string CurrentPassword
);

public sealed class TwoFactorDisableHandler(AppDbContext dbContext)
    : IRequestHandler<TwoFactorDisableInternalRequest, Result<bool>>, IValidatable<TwoFactorDisableInternalRequest>
{
    public Error? Validate(TwoFactorDisableInternalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return Error.Validation("VALIDATION_FAILED", "Current password is required");

        return null;
    }

    public async Task<Result<bool>> Handle(TwoFactorDisableInternalRequest request, CancellationToken cancellationToken)
    {
        // Find user
        var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        if (user == null)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        // Verify current password (required for security) - offloaded to Task.Run to avoid thread pool starvation
        if (!await Task.Run(() => BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash)))
        {
            return Error.Validation("INVALID_PASSWORD", "Current password is incorrect");
        }

        // Disable 2FA
        user.TwoFactorEnabled = false;

        // Delete all backup codes for this user
        var backupCodes = await dbContext.TwoFactorBackupCodes
            .Where(bc => bc.UserId == request.UserId)
            .ToListAsync(cancellationToken);
        dbContext.TwoFactorBackupCodes.RemoveRange(backupCodes);

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/2fa/disable", async (
                [FromBody] TwoFactorDisableRequest request,
                [FromServices] ICurrentUserService currentUserService,
                [FromServices] TwoFactorDisableHandler handler,
                CancellationToken ct) =>
            {
                var userIdResult = currentUserService.GetCurrentUserId();
                if (userIdResult.IsFailure)
                    return Results.Problem(statusCode: userIdResult.Error.StatusCode, title: userIdResult.Error.Code, detail: userIdResult.Error.Message);
                var userId = userIdResult.Value;

                var command = new TwoFactorDisableInternalRequest(userId, request.CurrentPassword);
                return await handler.ExecuteAsync(command, ct, success => Results.Ok(new { disabled = true }));
            })
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("TwoFactorDisable")
            .WithTags("Auth");
    }
}
