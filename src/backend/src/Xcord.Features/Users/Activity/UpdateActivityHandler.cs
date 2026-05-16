using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Users;

public sealed record UpdateActivityCommand(string ActivityType, string Name, string? Details, string? State, string? LargeImageUrl, string? SmallImageUrl);
public sealed record UpdateActivityRequest(string ActivityType, string Name, string? Details, string? State, string? LargeImageUrl, string? SmallImageUrl);
public sealed record ActivityResponse(long Id, long UserId, string ActivityType, string Name, string? Details, string? State, string? LargeImageUrl, string? SmallImageUrl, DateTimeOffset? StartedAt);

public sealed class UpdateActivityHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateActivityCommand, Result<ActivityResponse>>, IValidatable<UpdateActivityCommand>
{
    public Error? Validate(UpdateActivityCommand r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return Error.Validation("VALIDATION_ERROR", "Name is required");
        if (r.Name.Length > 128) return Error.Validation("VALIDATION_ERROR", "Name must be 128 characters or less");
        return null;
    }

    public async Task<Result<ActivityResponse>> Handle(UpdateActivityCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        if (!Enum.TryParse<ActivityType>(request.ActivityType, true, out var activityType))
            return Error.Validation("INVALID_TYPE", "Invalid activity type");

        var existing = await dbContext.UserActivities.FirstOrDefaultAsync(a => a.UserId == userId, ct).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        if (existing != null)
        {
            existing.ActivityType = activityType;
            existing.Name = request.Name;
            existing.Details = request.Details;
            existing.State = request.State;
            existing.LargeImageUrl = request.LargeImageUrl;
            existing.SmallImageUrl = request.SmallImageUrl;
            existing.StartedAt = now;
        }
        else
        {
            existing = new UserActivity
            {
                Id = snowflakeGenerator.NextId(), UserId = userId, ActivityType = activityType,
                Name = request.Name, Details = request.Details, State = request.State,
                LargeImageUrl = request.LargeImageUrl, SmallImageUrl = request.SmallImageUrl,
                StartedAt = now, CreatedAt = now
            };
            dbContext.UserActivities.Add(existing);
        }
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return new ActivityResponse(existing.Id, userId, existing.ActivityType.ToString(), existing.Name,
            existing.Details, existing.State, existing.LargeImageUrl, existing.SmallImageUrl, existing.StartedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/users/@me/activity", async (
            UpdateActivityRequest request,
            IRequestHandler<UpdateActivityCommand, Result<ActivityResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new UpdateActivityCommand(request.ActivityType, request.Name, request.Details,
                request.State, request.LargeImageUrl, request.SmallImageUrl), ct))
        .RequireAuthorization(Policies.User)
        .WithName("UpdateActivity").WithTags("Activity");
}
