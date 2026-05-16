using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Users;

public sealed record UpdateUserProfileRequest(
    string? DisplayName,
    string? Bio
);

public sealed class UpdateUserProfileHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateUserProfileRequest, Result<UserProfileDto>>, IValidatable<UpdateUserProfileRequest>
{
    public Error? Validate(UpdateUserProfileRequest request)
    {
        if (request.DisplayName != null && request.DisplayName.Length > 32)
        {
            return Error.Validation("VALIDATION_ERROR", "Display name must not exceed 32 characters");
        }

        if (request.Bio != null && request.Bio.Length > 190)
        {
            return Error.Validation("VALIDATION_ERROR", "Bio must not exceed 190 characters");
        }

        return null;
    }

    public async Task<Result<UserProfileDto>> Handle(UpdateUserProfileRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        // Only update non-null fields (partial update)
        if (request.DisplayName != null)
        {
            user.DisplayName = request.DisplayName;
        }

        if (request.Bio != null)
        {
            user.Bio = request.Bio;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UserProfileDto(
            UserId: user.Id,
            Username: user.Username,
            DisplayName: user.DisplayName,
            Bio: user.Bio,
            AvatarUrl: user.AvatarUrl,
            CreatedAt: user.CreatedAt,
            TwoFactorEnabled: user.TwoFactorEnabled
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPatch("/api/v1/users/@me", async (
            [FromBody] UpdateUserProfileRequest request,
            [FromServices] UpdateUserProfileHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(request, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("UpdateUserProfile")
        .WithTags("Users");
}
