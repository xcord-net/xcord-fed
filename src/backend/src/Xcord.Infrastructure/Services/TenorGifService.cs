using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Tenor GIF service implementation.
/// Proxies all requests through the server to prevent API key exposure and user IP tracking.
/// </summary>
public sealed class TenorGifService : IGifService
{
    private readonly HttpClient _httpClient;
    private readonly GifOptions _options;
    private readonly ILogger<TenorGifService> _logger;

    public TenorGifService(
        IHttpClientFactory httpClientFactory,
        IOptions<GifOptions> options,
        ILogger<TenorGifService> logger)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(TenorGifService));
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://tenor.googleapis.com/v2/");
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<GifSearchResult> SearchAsync(string query, int limit = 25)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Tenor API key is not configured");
            return new GifSearchResult(Array.Empty<GifItem>());
        }

        try
        {
            var url = $"search?q={Uri.EscapeDataString(query)}&key={_options.ApiKey}&limit={limit}&media_filter=gif";
            var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = ParseTenorResponse(json);

            _logger.LogInformation("Tenor search for '{Query}' returned {Count} results", query, result.Items.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Tenor GIFs for query '{Query}'", query);
            return new GifSearchResult(Array.Empty<GifItem>());
        }
    }

    public async Task<GifSearchResult> TrendingAsync(int limit = 25)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Tenor API key is not configured");
            return new GifSearchResult(Array.Empty<GifItem>());
        }

        try
        {
            var url = $"featured?key={_options.ApiKey}&limit={limit}&media_filter=gif";
            var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = ParseTenorResponse(json);

            _logger.LogInformation("Tenor trending returned {Count} results", result.Items.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch trending Tenor GIFs");
            return new GifSearchResult(Array.Empty<GifItem>());
        }
    }

    private GifSearchResult ParseTenorResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("results", out var resultsArray))
        {
            return new GifSearchResult(Array.Empty<GifItem>());
        }

        var items = new List<GifItem>();

        foreach (var result in resultsArray.EnumerateArray())
        {
            // Get ID
            if (!result.TryGetProperty("id", out var idElement))
                continue;
            var id = idElement.GetString();
            if (string.IsNullOrEmpty(id))
                continue;

            // Get title (content_description)
            var title = result.TryGetProperty("content_description", out var titleElement)
                ? titleElement.GetString() ?? ""
                : "";

            // Get media formats
            if (!result.TryGetProperty("media_formats", out var mediaFormats))
                continue;

            // Try to get GIF format
            if (!mediaFormats.TryGetProperty("gif", out var gifFormat))
                continue;

            // Get URL
            if (!gifFormat.TryGetProperty("url", out var urlElement))
                continue;
            var url = urlElement.GetString();
            if (string.IsNullOrEmpty(url))
                continue;

            // Get preview URL (nanogif or tinygif)
            var previewUrl = url;
            if (mediaFormats.TryGetProperty("nanogif", out var nanogifFormat) &&
                nanogifFormat.TryGetProperty("url", out var nanoUrlElement))
            {
                previewUrl = nanoUrlElement.GetString() ?? url;
            }
            else if (mediaFormats.TryGetProperty("tinygif", out var tinygifFormat) &&
                     tinygifFormat.TryGetProperty("url", out var tinyUrlElement))
            {
                previewUrl = tinyUrlElement.GetString() ?? url;
            }

            // Get dimensions
            var width = gifFormat.TryGetProperty("dims", out var dimsArray) &&
                       dimsArray.GetArrayLength() >= 2 &&
                       dimsArray[0].TryGetInt32(out var w) ? w : 0;

            var height = gifFormat.TryGetProperty("dims", out var dimsArray2) &&
                        dimsArray2.GetArrayLength() >= 2 &&
                        dimsArray2[1].TryGetInt32(out var h) ? h : 0;

            // Validate URLs before passthrough; drop entry entirely if either URL is invalid.
            if (!IsValidGifUrl(url) || !IsValidGifUrl(previewUrl))
            {
                _logger.LogWarning("Dropping Tenor result {Id} due to invalid URL", id);
                continue;
            }

            items.Add(new GifItem(id, title, url, previewUrl, width, height));
        }

        return new GifSearchResult(items);
    }

    /// <summary>
    /// Validates that a URL is a safe HTTPS URL pointing at a known Tenor host.
    /// Rejects null/empty, non-absolute URLs, non-https schemes (no data:, javascript:, http:),
    /// and hosts outside the Tenor allowlist.
    /// </summary>
    private static bool IsValidGifUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, "https", StringComparison.Ordinal))
            return false;

        var host = uri.Host;
        if (string.IsNullOrEmpty(host))
            return false;

        // Allowlist Tenor hostnames. Tenor serves media from tenor.com and *.tenor.com
        // (e.g. media.tenor.com, media1.tenor.com, c.tenor.com). The Google-hosted
        // tenor.googleapis.com endpoint is API-only and never appears in returned URLs.
        return host.Equals("tenor.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".tenor.com", StringComparison.OrdinalIgnoreCase);
    }
}
