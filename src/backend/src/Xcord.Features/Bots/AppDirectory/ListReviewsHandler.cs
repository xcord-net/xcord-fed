using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Bots;

public sealed record ListReviewsQuery(long AppId);

public sealed class ListReviewsHandler(AppDbContext dbContext)
    : IRequestHandler<ListReviewsQuery, Result<List<ReviewResponse>>>
{
    public async Task<Result<List<ReviewResponse>>> Handle(ListReviewsQuery request, CancellationToken ct)
    {
        var reviews = await dbContext.AppReviews.AsNoTracking()
            .Where(r => r.AppListingId == request.AppId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new ReviewResponse(r.Id, r.AppListingId, r.UserId, r.Rating, r.Content, r.CreatedAt))
            .ToListAsync(ct);
        return reviews;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/app-directory/{appId}/reviews", async (
            long appId,
            IRequestHandler<ListReviewsQuery, Result<List<ReviewResponse>>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ListReviewsQuery(appId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ListReviews").WithTags("AppDirectory");
}
