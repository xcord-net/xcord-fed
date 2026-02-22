namespace Xcord.Infrastructure.Services;

/// <summary>
/// No-op GIF service implementation that returns empty results.
/// Used when GIF search is disabled (provider = "none").
/// </summary>
public sealed class NoOpGifService : IGifService
{
    public Task<GifSearchResult> SearchAsync(string query, int limit = 25)
    {
        return Task.FromResult(new GifSearchResult(Array.Empty<GifItem>()));
    }

    public Task<GifSearchResult> TrendingAsync(int limit = 25)
    {
        return Task.FromResult(new GifSearchResult(Array.Empty<GifItem>()));
    }
}
