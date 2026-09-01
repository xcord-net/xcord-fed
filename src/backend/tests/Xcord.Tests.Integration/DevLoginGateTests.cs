using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// The dev login route signs a caller in as the instance admin with no
/// credentials at all. The only thing standing between that and a production
/// instance is the TestSeed:Key gate in Program.cs, so these tests pin the
/// closed direction: with no key configured (as in this fixture, and as in
/// production) the route must not exist, and /api/v1/config must not advertise
/// it to the login page.
/// </summary>
[Collection("WebApp")]
public class DevLoginGateTests
{
    private readonly WebAppFixture _fixture;

    public DevLoginGateTests(WebAppFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DevLogin_WithoutTestSeedKey_IssuesNoSession()
    {
        var response = await _fixture.Client.PostAsync("/api/v1/test/dev-login", null);

        // The exact status is a routing detail - the SPA fallback claims the
        // path for GET, so an unmapped POST surfaces as 405 rather than 404.
        // What must hold is that no session comes back.
        response.IsSuccessStatusCode.Should().BeFalse(
            "TestSeedEndpoint is only mapped when TestSeed:Key is configured");
        response.Headers.TryGetValues("Set-Cookie", out var cookies);
        (cookies ?? Enumerable.Empty<string>())
            .Should().NotContain(c => c.Contains("access_token") || c.Contains("refresh_token"),
                "an unmapped dev login route must not authenticate anyone");
    }

    [Fact]
    public async Task GetConfig_WithoutTestSeedKey_ReportsDevLoginDisabled()
    {
        var response = await _fixture.Client.GetAsync("/api/v1/config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.TryGetProperty("devLoginEnabled", out var devLogin)
            .Should().BeTrue("the login page reads this flag to decide whether to render the dev button");
        devLogin.GetBoolean().Should().BeFalse("no TestSeed:Key is configured");
    }
}
