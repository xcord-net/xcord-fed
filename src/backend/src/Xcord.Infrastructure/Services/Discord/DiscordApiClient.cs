using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;

namespace Xcord.Infrastructure.Services.Discord;

/// <summary>
/// Thin HTTP client wrapper for the Discord REST API v10.
/// </summary>
public sealed class DiscordApiClient
{
    private const string BaseUrl = "https://discord.com/api/v10";

    private readonly HttpClient _http;
    private readonly DiscordRateLimiter _rateLimiter;

    public DiscordApiClient(HttpClient http, DiscordRateLimiter rateLimiter)
    {
        _http = http;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Sets the Authorization header to "Bot {token}" for all subsequent requests.
    /// </summary>
    public void SetToken(string botToken)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bot", botToken);
    }

    // -------------------------------------------------------------------------
    // Guild
    // -------------------------------------------------------------------------

    public Task<JsonElement> GetGuildAsync(string guildId, CancellationToken ct) =>
        GetJsonAsync($"{BaseUrl}/guilds/{guildId}", ct);

    public Task<JsonElement> GetGuildChannelsAsync(string guildId, CancellationToken ct) =>
        GetJsonAsync($"{BaseUrl}/guilds/{guildId}/channels", ct);

    public Task<JsonElement> GetGuildRolesAsync(string guildId, CancellationToken ct) =>
        GetJsonAsync($"{BaseUrl}/guilds/{guildId}/roles", ct);

    public Task<JsonElement> GetGuildEmojisAsync(string guildId, CancellationToken ct) =>
        GetJsonAsync($"{BaseUrl}/guilds/{guildId}/emojis", ct);

    // -------------------------------------------------------------------------
    // Members (paginated)
    // -------------------------------------------------------------------------

    public Task<JsonElement> GetGuildMembersAsync(
        string guildId,
        int limit = 1000,
        string? after = null,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/guilds/{guildId}/members?limit={limit}";
        if (after is not null)
            url += $"&after={after}";
        return GetJsonAsync(url, ct);
    }

    // -------------------------------------------------------------------------
    // Messages (paginated)
    // -------------------------------------------------------------------------

    public Task<JsonElement> GetChannelMessagesAsync(
        string channelId,
        int limit = 100,
        string? before = null,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/channels/{channelId}/messages?limit={limit}";
        if (before is not null)
            url += $"&before={before}";
        return GetJsonAsync(url, ct);
    }

    // -------------------------------------------------------------------------
    // Reactions (paginated)
    // -------------------------------------------------------------------------

    public Task<JsonElement> GetMessageReactionsAsync(
        string channelId,
        string messageId,
        string emoji,
        int limit = 100,
        string? after = null,
        CancellationToken ct = default)
    {
        var encodedEmoji = HttpUtility.UrlEncode(emoji);
        var url = $"{BaseUrl}/channels/{channelId}/messages/{messageId}/reactions/{encodedEmoji}?limit={limit}";
        if (after is not null)
            url += $"&after={after}";
        return GetJsonAsync(url, ct);
    }

    // -------------------------------------------------------------------------
    // Threads
    // -------------------------------------------------------------------------

    public Task<JsonElement> GetActiveThreadsAsync(string guildId, CancellationToken ct) =>
        GetJsonAsync($"{BaseUrl}/guilds/{guildId}/threads/active", ct);

    public Task<JsonElement> GetArchivedThreadsAsync(string channelId, CancellationToken ct) =>
        GetJsonAsync($"{BaseUrl}/channels/{channelId}/threads/archived/public", ct);

    public Task<JsonElement> GetThreadMembersAsync(string threadId, CancellationToken ct) =>
        GetJsonAsync($"{BaseUrl}/channels/{threadId}/thread-members", ct);

    // -------------------------------------------------------------------------
    // Binary download (CDN assets — no auth header required)
    // -------------------------------------------------------------------------

    public async Task<Stream> DownloadAsync(string url, CancellationToken ct)
    {
        await _rateLimiter.WaitAsync(ct).ConfigureAwait(false);

        // Use a request message so we can omit the Authorization header for CDN URLs
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = null;

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        return await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<JsonElement> GetJsonAsync(string url, CancellationToken ct)
    {
        while (true)
        {
            await _rateLimiter.WaitAsync(ct).ConfigureAwait(false);

            var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.TryGetValues("Retry-After", out var values)
                        && double.TryParse(values.FirstOrDefault(), System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var seconds)
                        ? TimeSpan.FromSeconds(seconds)
                        : TimeSpan.FromSeconds(1));

                await Task.Delay(retryAfter, ct).ConfigureAwait(false);
                continue;
            }

            await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            return doc.RootElement.Clone();
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        switch ((int)response.StatusCode)
        {
            case 401:
                throw new UnauthorizedAccessException("Invalid Discord bot token");
            case 403:
                throw new InvalidOperationException("Bot lacks required permissions for this guild");
            case 404:
                throw new InvalidOperationException("Guild not found or bot is not a member");
            default:
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"Discord API returned {(int)response.StatusCode}: {body}",
                    inner: null,
                    statusCode: response.StatusCode);
        }
    }
}
