using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Bots.AppDirectory;

public sealed record ListAppsQuery(string? Search, string? Category);
public sealed record AppListingResponse(long Id, string Name, string? ShortDescription, string? IconUrl, string? Category, int InstallCount, bool IsVerified, double? AverageRating);

public sealed class ListAppsHandler(AppDbContext dbContext)
    : IRequestHandler<ListAppsQuery, Result<List<AppListingResponse>>>
{
    public async Task<Result<List<AppListingResponse>>> Handle(ListAppsQuery request, CancellationToken ct)
    {
        var query = dbContext.AppListings.AsNoTracking().Where(a => a.IsPublished);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(a => a.Name.Contains(request.Search) || (a.ShortDescription != null && a.ShortDescription.Contains(request.Search)));

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(a => a.Category == request.Category);

        var apps = await query.OrderByDescending(a => a.InstallCount).Take(50)
            .Select(a => new AppListingResponse(a.Id, a.Name, a.ShortDescription, a.IconUrl, a.Category, a.InstallCount, a.IsVerified,
                a.Reviews.Any() ? a.Reviews.Average(r => r.Rating) : (double?)null))
            .ToListAsync(ct);
        return apps;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/app-directory", async (
            string? search, string? category,
            IRequestHandler<ListAppsQuery, Result<List<AppListingResponse>>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ListAppsQuery(search, category), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ListApps").WithTags("AppDirectory");
}
