using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Users;

public sealed record CancelAccountDeletionRequest;

public sealed class CancelAccountDeletionHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<CancelAccountDeletionRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        CancelAccountDeletionRequest request,
        CancellationToken cancellationToken)
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

        if (user.ScheduledDeletionAt is null)
        {
            return Error.Validation("NO_DELETION_SCHEDULED", "No account deletion is scheduled");
        }

        user.ScheduledDeletionAt = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/users/@me/cancel-deletion", async (
            [FromServices] CancelAccountDeletionHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new CancelAccountDeletionRequest(), ct,
                _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("CancelAccountDeletion")
        .WithTags("Users");
}
