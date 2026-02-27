using BCrypt.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Users;

public sealed record ScheduleAccountDeletionRequest(string Password);

public sealed record ScheduleAccountDeletionResponse(
    DateTimeOffset ScheduledDeletionAt
);

public sealed class ScheduleAccountDeletionHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ScheduleAccountDeletionRequest, Result<ScheduleAccountDeletionResponse>>,
      IValidatable<ScheduleAccountDeletionRequest>
{
    public Error? Validate(ScheduleAccountDeletionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Error.Validation("VALIDATION_ERROR", "Password is required to confirm account deletion");
        }

        return null;
    }

    public async Task<Result<ScheduleAccountDeletionResponse>> Handle(
        ScheduleAccountDeletionRequest request,
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

        // Verify password — offloaded to Task.Run to avoid thread pool starvation
        if (!await Task.Run(() => BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)))
        {
            return Error.Validation("INVALID_PASSWORD", "Password is incorrect");
        }

        // Schedule deletion 14 days from now
        user.ScheduledDeletionAt = DateTimeOffset.UtcNow.AddDays(14);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ScheduleAccountDeletionResponse(user.ScheduledDeletionAt.Value);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/users/@me/delete", async (
            [FromBody] ScheduleAccountDeletionRequest request,
            [FromServices] ScheduleAccountDeletionHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(request, ct,
                success => Results.Ok(success));
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("ScheduleAccountDeletion")
        .WithTags("Users");
}
