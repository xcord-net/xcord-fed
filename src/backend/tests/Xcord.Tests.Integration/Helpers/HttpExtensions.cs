using System.Net.Http.Json;
using System.Text.Json;

namespace Xcord.Tests.Integration.Helpers;

public static class HttpExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<T> ReadAsJsonAsync<T>(this HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize response: {content}");
    }

    public static async Task<HttpResponseMessage> PostJsonAsync(
        this HttpClient client, string url, object body)
    {
        return await client.PostAsJsonAsync(url, body, JsonOptions);
    }

    public static async Task<HttpResponseMessage> PatchJsonAsync(
        this HttpClient client, string url, object body)
    {
        return await client.PatchAsJsonAsync(url, body, JsonOptions);
    }

    public static async Task<HttpResponseMessage> PutJsonAsync(
        this HttpClient client, string url, object body)
    {
        return await client.PutAsJsonAsync(url, body, JsonOptions);
    }

    /// <summary>
    /// Reads a long value from a JsonElement, handling both number and string representations.
    /// SnowflakeJsonConverter serializes longs as strings for JS Number.MAX_SAFE_INTEGER safety.
    /// </summary>
    public static long ReadLong(this JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String
            ? long.Parse(element.GetString()!)
            : element.GetInt64();
    }
}
