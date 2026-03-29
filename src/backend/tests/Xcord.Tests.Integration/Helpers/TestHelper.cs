using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xcord.Tests.Integration.Fixtures;

namespace Xcord.Tests.Integration.Helpers;

/// <summary>
/// Shared helper for integration tests - registers users, creates servers/channels,
/// and provides authenticated HTTP clients.
/// </summary>
public sealed class TestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly WebAppFixture _fixture;

    public TestHelper(WebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Extracts the access_token from the Set-Cookie header in an HTTP response.
    /// </summary>
    public static string ExtractAccessTokenFromCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            throw new InvalidOperationException("No Set-Cookie header found in response");

        foreach (var cookie in cookies)
        {
            if (cookie.StartsWith("access_token=", StringComparison.OrdinalIgnoreCase))
            {
                var value = cookie.Split(';')[0]; // "access_token=<jwt>"
                return value["access_token=".Length..];
            }
        }

        throw new InvalidOperationException("No access_token cookie found in Set-Cookie header");
    }

    /// <summary>
    /// Registers a new user, confirms their email, and returns their details + access token
    /// with email_confirmed=true (required by default authorization policy).
    /// </summary>
    public async Task<AuthenticatedUser> RegisterUserAsync(
        string? username = null, string? email = null, string? password = null)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        username ??= $"user_{id}";
        email ??= $"{id}@xcord.local";
        password ??= "TestPassword123!";

        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username,
            displayName = $"Test {username}",
            email,
            password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "registration should succeed");

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var userId = ParseSnowflakeId(body.GetProperty("userId"));
        var initialToken = ExtractAccessTokenFromCookies(response);

        // Confirm email via DB + confirm-email endpoint to get a valid email_confirmed=true token
        string confirmationCode;
        await using (var db = _fixture.CreateDbContext())
        {
            var token = await db.EmailConfirmationTokens
                .FirstOrDefaultAsync(t => t.UserId == userId);
            token.Should().NotBeNull("a confirmation token should exist after registration");
            confirmationCode = token!.Code;
        }

        var confirmRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/confirm-email?code={confirmationCode}");
        confirmRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", initialToken);
        var confirmResponse = await _fixture.Client.SendAsync(confirmRequest);
        confirmResponse.EnsureSuccessStatusCode();

        // Re-login to get a fresh access token with email_confirmed=true
        var loginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new { email, password });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "login after email confirmation should succeed");

        return new AuthenticatedUser
        {
            UserId = userId,
            Username = body.GetProperty("username").GetString()!,
            Email = email,
            Password = password,
            AccessToken = ExtractAccessTokenFromCookies(loginResponse),
        };
    }

    /// <summary>
    /// Registers a new user WITHOUT confirming email. Returns token with email_confirmed=false.
    /// Use this only for tests that specifically test unconfirmed user behavior.
    /// </summary>
    public async Task<AuthenticatedUser> RegisterUnconfirmedUserAsync(
        string? username = null, string? email = null, string? password = null)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        username ??= $"user_{id}";
        email ??= $"{id}@xcord.local";
        password ??= "TestPassword123!";

        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username,
            displayName = $"Test {username}",
            email,
            password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "registration should succeed");

        var body = await response.ReadAsJsonAsync<JsonElement>();
        return new AuthenticatedUser
        {
            UserId = ParseSnowflakeId(body.GetProperty("userId")),
            Username = body.GetProperty("username").GetString()!,
            Email = email,
            Password = password,
            AccessToken = ExtractAccessTokenFromCookies(response),
        };
    }

    /// <summary>
    /// Parses a Snowflake ID from JSON, handling both number and string representations.
    /// </summary>
    public static long ParseSnowflakeId(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String
            ? long.Parse(element.GetString()!)
            : element.GetInt64();
    }

    /// <summary>
    /// Creates a server as the given user. Returns the server details.
    /// </summary>
    public async Task<JsonElement> CreateServerAsync(string accessToken, string? name = null)
    {
        name ??= $"Server_{Guid.NewGuid():N}"[..20];

        var request = AuthRequest(HttpMethod.Post, "/api/v1/servers", accessToken);
        request.Content = JsonContent.Create(new { name }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"server creation should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Creates a text channel in a server.
    /// </summary>
    public async Task<JsonElement> CreateChannelAsync(
        string accessToken, long serverId, string? name = null, int type = 0)
    {
        name ??= $"channel-{Guid.NewGuid():N}"[..20];

        var request = AuthRequest(HttpMethod.Post, $"/api/v1/servers/{serverId}/channels", accessToken);
        request.Content = JsonContent.Create(new { name, type }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"channel creation should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Sends a message to a conversation (channel).
    /// </summary>
    public async Task<JsonElement> SendMessageAsync(
        string accessToken, long conversationId, string content = "Test message")
    {
        var request = AuthRequest(HttpMethod.Post,
            $"/api/v1/conversations/{conversationId}/messages", accessToken);
        request.Content = JsonContent.Create(new { content }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"sending message should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Creates an invite for a server.
    /// </summary>
    public async Task<string> CreateInviteAsync(string accessToken, long serverId)
    {
        var request = AuthRequest(HttpMethod.Post, $"/api/v1/servers/{serverId}/invites", accessToken);
        request.Content = JsonContent.Create(new { }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"invite creation should succeed: {await response.Content.ReadAsStringAsync()}");

        var body = await response.ReadAsJsonAsync<JsonElement>();
        return body.GetProperty("code").GetString()!;
    }

    /// <summary>
    /// Joins a server using an invite code as the given user.
    /// </summary>
    public async Task<HttpResponseMessage> JoinServerAsync(string accessToken, string inviteCode)
    {
        var request = AuthRequest(HttpMethod.Post, $"/api/v1/invites/{inviteCode}/accept", accessToken);
        return await _fixture.Client.SendAsync(request);
    }

    /// <summary>
    /// Creates an authenticated HTTP request.
    /// </summary>
    public static HttpRequestMessage AuthRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    /// <summary>
    /// Sends an authenticated GET request and returns the response.
    /// </summary>
    public async Task<HttpResponseMessage> AuthGetAsync(string url, string accessToken)
    {
        var request = AuthRequest(HttpMethod.Get, url, accessToken);
        return await _fixture.Client.SendAsync(request);
    }

    /// <summary>
    /// Sends an authenticated DELETE request and returns the response.
    /// </summary>
    public async Task<HttpResponseMessage> AuthDeleteAsync(string url, string accessToken)
    {
        var request = AuthRequest(HttpMethod.Delete, url, accessToken);
        return await _fixture.Client.SendAsync(request);
    }

    /// <summary>
    /// Sends an authenticated POST request with JSON body.
    /// </summary>
    public async Task<HttpResponseMessage> AuthPostAsync(string url, string accessToken, object? body = null)
    {
        var request = AuthRequest(HttpMethod.Post, url, accessToken);
        if (body != null)
            request.Content = JsonContent.Create(body, options: JsonOptions);
        return await _fixture.Client.SendAsync(request);
    }

    /// <summary>
    /// Sends an authenticated PATCH request with JSON body.
    /// </summary>
    public async Task<HttpResponseMessage> AuthPatchAsync(string url, string accessToken, object body)
    {
        var request = AuthRequest(HttpMethod.Patch, url, accessToken);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        return await _fixture.Client.SendAsync(request);
    }
}

public sealed class AuthenticatedUser
{
    public long UserId { get; init; }
    public string Username { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
    public string AccessToken { get; init; } = null!;
}
