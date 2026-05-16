using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Users;

public sealed record GetUserProfileRequest;

public sealed record UserProfileDto(
    long UserId,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    DateTimeOffset CreatedAt,
    bool TwoFactorEnabled = false,
    DateTimeOffset? ScheduledDeletionAt = null
);

public sealed class GetUserProfileHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetUserProfileRequest, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> Handle(GetUserProfileRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("USER_NOT_FOUND", "User not found");
        }

        return new UserProfileDto(
            UserId: user.Id,
            Username: user.Username,
            DisplayName: user.DisplayName,
            Bio: user.Bio,
            AvatarUrl: user.AvatarUrl,
            CreatedAt: user.CreatedAt,
            TwoFactorEnabled: user.TwoFactorEnabled,
            ScheduledDeletionAt: user.ScheduledDeletionAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/@me", async (
            [FromServices] GetUserProfileHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new GetUserProfileRequest(), ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("GetUserProfile")
        .WithTags("Users");
}
