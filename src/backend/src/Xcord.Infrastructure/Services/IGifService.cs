namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for searching and retrieving GIFs from external providers.
/// All requests are proxied through the server to prevent API key exposure and user IP tracking.
/// </summary>
public interface IGifService
{
    /// <summary>
    /// Search for GIFs matching a query.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="limit">Maximum number of results (default: 25)</param>
    /// <returns>Search results</returns>
    Task<GifSearchResult> SearchAsync(string query, int limit = 25);

    /// <summary>
    /// Get trending GIFs.
    /// </summary>
    /// <param name="limit">Maximum number of results (default: 25)</param>
    /// <returns>Trending GIFs</returns>
    Task<GifSearchResult> TrendingAsync(int limit = 25);
}

/// <summary>
/// Result of a GIF search or trending query.
/// </summary>
public sealed record GifSearchResult(IReadOnlyList<GifItem> Items);

/// <summary>
/// A single GIF item from search results.
/// </summary>
public sealed record GifItem(
    string Id,
    string Title,
    string Url,
    string PreviewUrl,
    int Width,
    int Height
);
