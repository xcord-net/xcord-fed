using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Auth;

public sealed class AuthMeHandler : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/auth/me", async (
                [FromServices] AppDbContext dbContext,
                [FromServices] ICurrentUserService currentUserService,
                CancellationToken ct) =>
            {
                var userIdResult = currentUserService.GetCurrentUserId();
                if (userIdResult.IsFailure)
                    return Results.Problem(
                        statusCode: userIdResult.Error.StatusCode,
                        title: userIdResult.Error.Code,
                        detail: userIdResult.Error.Message);

                var userId = userIdResult.Value;
                var user = await dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId, ct);

                if (user == null)
                    return Results.NotFound();

                return Results.Ok(new UserInfoResponse(
                    user.Id,
                    user.Username,
                    user.DisplayName,
                    user.AvatarUrl,
                    user.EmailConfirmed,
                    user.TwoFactorEnabled,
                    user.IsAdmin,
                    user.IsBot));
            })
            .RequireAuthorization()
            .WithName("AuthMe")
            .WithTags("Auth");
    }
}
