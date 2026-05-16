using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Gifs;

public sealed record SearchGifsQuery(
    string Query,
    int Limit
);

public sealed record SearchGifsResponse(
    List<GifDto> Gifs
);

public sealed record GifDto(
    string Id,
    string Title,
    string Url,
    string PreviewUrl,
    int Width,
    int Height
);

public sealed class SearchGifsHandler(
    IGifService gifService,
    ILogger<SearchGifsHandler> logger)
    : IRequestHandler<SearchGifsQuery, Result<SearchGifsResponse>>, IValidatable<SearchGifsQuery>
{
    public Error? Validate(SearchGifsQuery request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Error.Validation("VALIDATION_ERROR", "Query is required");
        }

        if (request.Query.Length > 200)
        {
            return Error.Validation("VALIDATION_ERROR", "Query cannot exceed 200 characters");
        }

        if (request.Limit < 1 || request.Limit > 50)
        {
            return Error.Validation("VALIDATION_ERROR", "Limit must be between 1 and 50");
        }

        return null;
    }

    public async Task<Result<SearchGifsResponse>> Handle(SearchGifsQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Searching GIFs for query '{Query}' with limit {Limit}", request.Query, request.Limit);

        var result = await gifService.SearchAsync(request.Query, request.Limit).ConfigureAwait(false);

        var gifs = result.Items.Select(g => new GifDto(
            g.Id,
            g.Title,
            g.Url,
            g.PreviewUrl,
            g.Width,
            g.Height
        )).ToList();

        return new SearchGifsResponse(gifs);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/gifs/search", async (
            string query,
            int limit,
            IRequestHandler<SearchGifsQuery, Result<SearchGifsResponse>> handler,
            CancellationToken ct) =>
        {
            var searchQuery = new SearchGifsQuery(query, limit);
            return await handler.ExecuteAsync(searchQuery, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("SearchGifs")
        .WithTags("Gifs");
    }
}
