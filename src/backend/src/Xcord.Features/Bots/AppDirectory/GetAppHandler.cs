using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Bots.AppDirectory;

public sealed record GetAppQuery(long AppId);
public sealed record AppDetailResponse(long Id, string Name, string? Description, string? ShortDescription, string? IconUrl, string? Category, string? Tags, int InstallCount, bool IsVerified, DateTimeOffset CreatedAt, double? AverageRating, int ReviewCount);

public sealed class GetAppHandler(AppDbContext dbContext)
    : IRequestHandler<GetAppQuery, Result<AppDetailResponse>>
{
    public async Task<Result<AppDetailResponse>> Handle(GetAppQuery request, CancellationToken ct)
    {
        var app = await dbContext.AppListings.AsNoTracking()
            .Where(a => a.Id == request.AppId && a.IsPublished)
            .Select(a => new AppDetailResponse(a.Id, a.Name, a.Description, a.ShortDescription, a.IconUrl,
                a.Category, a.Tags, a.InstallCount, a.IsVerified, a.CreatedAt,
                a.Reviews.Any() ? a.Reviews.Average(r => r.Rating) : (double?)null,
                a.Reviews.Count()))
            .FirstOrDefaultAsync(ct);

        if (app == null) return Error.NotFound("APP_NOT_FOUND", "App not found");
        return app;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/app-directory/{appId}", async (
            long appId,
            IRequestHandler<GetAppQuery, Result<AppDetailResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetAppQuery(appId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("GetApp").WithTags("AppDirectory");
}
