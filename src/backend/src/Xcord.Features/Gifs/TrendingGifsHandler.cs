using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Gifs;

public sealed record TrendingGifsQuery(
    int Limit
);

public sealed record TrendingGifsResponse(
    List<GifDto> Gifs
);

public sealed class TrendingGifsHandler(
    IGifService gifService,
    ILogger<TrendingGifsHandler> logger)
    : IRequestHandler<TrendingGifsQuery, Result<TrendingGifsResponse>>, IValidatable<TrendingGifsQuery>
{
    public Error? Validate(TrendingGifsQuery request)
    {
        if (request.Limit < 1 || request.Limit > 50)
        {
            return Error.Validation("VALIDATION_ERROR", "Limit must be between 1 and 50");
        }

        return null;
    }

    public async Task<Result<TrendingGifsResponse>> Handle(TrendingGifsQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching trending GIFs with limit {Limit}", request.Limit);

        var result = await gifService.TrendingAsync(request.Limit);

        var gifs = result.Items.Select(g => new GifDto(
            g.Id,
            g.Title,
            g.Url,
            g.PreviewUrl,
            g.Width,
            g.Height
        )).ToList();

        return new TrendingGifsResponse(gifs);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/gifs/trending", async (
            int limit,
            IRequestHandler<TrendingGifsQuery, Result<TrendingGifsResponse>> handler,
            CancellationToken ct) =>
        {
            var query = new TrendingGifsQuery(limit);
            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("TrendingGifs")
        .WithTags("Gifs");
    }
}
