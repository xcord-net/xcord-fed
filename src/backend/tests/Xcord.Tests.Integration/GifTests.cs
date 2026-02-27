using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Integration tests for GIF search and trending endpoints.
/// The test fixture uses Gif:Provider = "none" (NoOpGifService),
/// so these verify endpoint routing, auth, and validation work correctly.
/// </summary>
[Collection("WebApp")]
public class GifTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public GifTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Search GIFs ────────────

    [Fact]
    public async Task SearchGifs_ValidQuery_Returns200WithEmptyResults()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/gifs/search?query=cats&limit=10",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("gifs").EnumerateArray().Should().BeEmpty(
            "NoOpGifService returns empty results in test environment");
    }

    [Fact]
    public async Task SearchGifs_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.GetAsync(
            "/api/v1/gifs/search?query=cats&limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Trending GIFs ────────────

    [Fact]
    public async Task TrendingGifs_ValidLimit_Returns200WithEmptyResults()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/gifs/trending?limit=10",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("gifs").EnumerateArray().Should().BeEmpty(
            "NoOpGifService returns empty results in test environment");
    }

    [Fact]
    public async Task TrendingGifs_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.GetAsync(
            "/api/v1/gifs/trending?limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
