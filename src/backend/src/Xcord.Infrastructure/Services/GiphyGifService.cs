using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Giphy GIF service implementation.
/// Proxies all requests through the server to prevent API key exposure and user IP tracking.
/// </summary>
public sealed class GiphyGifService : IGifService
{
    private readonly HttpClient _httpClient;
    private readonly GifOptions _options;
    private readonly ILogger<GiphyGifService> _logger;

    public GiphyGifService(
        IHttpClientFactory httpClientFactory,
        IOptions<GifOptions> options,
        ILogger<GiphyGifService> logger)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(GiphyGifService));
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.giphy.com/v1/gifs/");
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<GifSearchResult> SearchAsync(string query, int limit = 25)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Giphy API key is not configured");
            return new GifSearchResult(Array.Empty<GifItem>());
        }

        try
        {
            var url = $"search?q={Uri.EscapeDataString(query)}&api_key={_options.ApiKey}&limit={limit}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = ParseGiphyResponse(json);

            _logger.LogInformation("Giphy search for '{Query}' returned {Count} results", query, result.Items.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Giphy GIFs for query '{Query}'", query);
            return new GifSearchResult(Array.Empty<GifItem>());
        }
    }

    public async Task<GifSearchResult> TrendingAsync(int limit = 25)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Giphy API key is not configured");
            return new GifSearchResult(Array.Empty<GifItem>());
        }

        try
        {
            var url = $"trending?api_key={_options.ApiKey}&limit={limit}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = ParseGiphyResponse(json);

            _logger.LogInformation("Giphy trending returned {Count} results", result.Items.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch trending Giphy GIFs");
            return new GifSearchResult(Array.Empty<GifItem>());
        }
    }

    private GifSearchResult ParseGiphyResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("data", out var dataArray))
        {
            return new GifSearchResult(Array.Empty<GifItem>());
        }

        var items = new List<GifItem>();

        foreach (var result in dataArray.EnumerateArray())
        {
            // Get ID
            if (!result.TryGetProperty("id", out var idElement))
                continue;
            var id = idElement.GetString();
            if (string.IsNullOrEmpty(id))
                continue;

            // Get title
            var title = result.TryGetProperty("title", out var titleElement)
                ? titleElement.GetString() ?? ""
                : "";

            // Get images
            if (!result.TryGetProperty("images", out var images))
                continue;

            // Try to get original GIF
            if (!images.TryGetProperty("original", out var original))
                continue;

            // Get URL
            if (!original.TryGetProperty("url", out var urlElement))
                continue;
            var url = urlElement.GetString();
            if (string.IsNullOrEmpty(url))
                continue;

            // Get preview URL (fixed_width_small or preview_gif)
            var previewUrl = url;
            if (images.TryGetProperty("fixed_width_small", out var fixedWidthSmall) &&
                fixedWidthSmall.TryGetProperty("url", out var previewUrlElement))
            {
                previewUrl = previewUrlElement.GetString() ?? url;
            }
            else if (images.TryGetProperty("preview_gif", out var previewGif) &&
                     previewGif.TryGetProperty("url", out var previewGifUrlElement))
            {
                previewUrl = previewGifUrlElement.GetString() ?? url;
            }

            // Get dimensions
            var width = 0;
            var height = 0;

            if (original.TryGetProperty("width", out var widthElement))
            {
                if (widthElement.ValueKind == JsonValueKind.String)
                    int.TryParse(widthElement.GetString(), out width);
                else if (widthElement.ValueKind == JsonValueKind.Number)
                    width = widthElement.GetInt32();
            }

            if (original.TryGetProperty("height", out var heightElement))
            {
                if (heightElement.ValueKind == JsonValueKind.String)
                    int.TryParse(heightElement.GetString(), out height);
                else if (heightElement.ValueKind == JsonValueKind.Number)
                    height = heightElement.GetInt32();
            }

            items.Add(new GifItem(id, title, url, previewUrl, width, height));
        }

        return new GifSearchResult(items);
    }
}
