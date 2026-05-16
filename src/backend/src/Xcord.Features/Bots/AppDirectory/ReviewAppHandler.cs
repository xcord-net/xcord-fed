using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Bots;

public sealed record ReviewAppCommand(long AppId, int Rating, string? Content);
public sealed record ReviewAppRequest(int Rating, string? Content);
public sealed record ReviewResponse(long Id, long AppListingId, long UserId, int Rating, string? Content, DateTimeOffset CreatedAt);

public sealed class ReviewAppHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService)
    : IRequestHandler<ReviewAppCommand, Result<ReviewResponse>>, IValidatable<ReviewAppCommand>
{
    public Error? Validate(ReviewAppCommand r)
    {
        if (r.Rating < 1 || r.Rating > 5) return Error.Validation("VALIDATION_ERROR", "Rating must be between 1 and 5");
        return null;
    }

    public async Task<Result<ReviewResponse>> Handle(ReviewAppCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var appExists = await dbContext.AppListings.AsNoTracking().AnyAsync(a => a.Id == request.AppId && a.IsPublished, ct).ConfigureAwait(false);
        if (!appExists) return Error.NotFound("APP_NOT_FOUND", "App not found");

        var existingReview = await dbContext.AppReviews.AsNoTracking()
            .AnyAsync(r => r.AppListingId == request.AppId && r.UserId == userId, ct);
        if (existingReview) return Error.Conflict("ALREADY_REVIEWED", "You have already reviewed this app");

        var now = DateTimeOffset.UtcNow;
        var review = new AppReview
        {
            Id = snowflakeGenerator.NextId(), AppListingId = request.AppId,
            UserId = userId, Rating = request.Rating, Content = request.Content, CreatedAt = now
        };
        dbContext.AppReviews.Add(review);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return new ReviewResponse(review.Id, review.AppListingId, userId, review.Rating, review.Content, review.CreatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/app-directory/{appId}/reviews", async (
            long appId, ReviewAppRequest request,
            IRequestHandler<ReviewAppCommand, Result<ReviewResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ReviewAppCommand(appId, request.Rating, request.Content), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ReviewApp").WithTags("AppDirectory");
}
