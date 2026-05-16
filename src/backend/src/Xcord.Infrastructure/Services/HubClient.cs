using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

public sealed class HubClient : IHubClient
{
    private readonly HubOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HubClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public HubClient(IOptions<HubOptions> options, HttpClient httpClient, ILogger<HubClient> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _logger = logger;
    }

    public async Task<HubVersionsResponse?> GetVersionsAsync(CancellationToken ct = default)
    {
        if (!IsConfigured()) return null;

        try
        {
            var request = CreateRequest(HttpMethod.Get, "versions");
            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<HubVersionsResponse>(content, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get versions from hub");
            return null;
        }
    }

    public async Task<bool> RequestUpgradeAsync(string targetVersion, CancellationToken ct = default)
    {
        if (!IsConfigured()) return false;

        try
        {
            var request = CreateRequest(HttpMethod.Post, "request-upgrade");
            var body = JsonSerializer.Serialize(new { targetVersion }, JsonOptions);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request upgrade from hub");
            return false;
        }
    }

    public async Task<bool> UpdateBatchPreferenceAsync(bool enabled, CancellationToken ct = default)
    {
        if (!IsConfigured()) return false;

        try
        {
            var request = CreateRequest(HttpMethod.Patch, "batch-upgrades");
            var body = JsonSerializer.Serialize(new { enabled }, JsonOptions);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update batch preference on hub");
            return false;
        }
    }

    public async Task<HubUpgradeHistoryResponse?> GetUpgradeHistoryAsync(CancellationToken ct = default)
    {
        if (!IsConfigured()) return null;

        try
        {
            var request = CreateRequest(HttpMethod.Get, "upgrade-history");
            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<HubUpgradeHistoryResponse>(content, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get upgrade history from hub");
            return null;
        }
    }

    private bool IsConfigured() =>
        _options.Enabled && !string.IsNullOrEmpty(_options.FederationToken) && !string.IsNullOrEmpty(_options.GatewayApiUrl);

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var url = $"{_options.GatewayApiUrl!.TrimEnd('/')}/api/v1/federation/{path}";
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.FederationToken);
        return request;
    }
}

public sealed class NullHubClient : IHubClient
{
    public Task<HubVersionsResponse?> GetVersionsAsync(CancellationToken ct = default) =>
        Task.FromResult<HubVersionsResponse?>(null);

    public Task<bool> RequestUpgradeAsync(string targetVersion, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<bool> UpdateBatchPreferenceAsync(bool enabled, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<HubUpgradeHistoryResponse?> GetUpgradeHistoryAsync(CancellationToken ct = default) =>
        Task.FromResult<HubUpgradeHistoryResponse?>(null);
}
