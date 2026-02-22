using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xcord.Infrastructure.Options;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Verifies that tokens signed by a different RSA key (i.e., a different instance)
/// are rejected. This is a fundamental security property of container-per-tenant.
/// </summary>
[Collection("WebApp")]
public class CrossServerTokenTests
{
    private readonly WebAppFixture _fixture;

    public CrossServerTokenTests(WebAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ForeignToken_ApiEndpoint_Returns401()
    {
        // Generate a token signed with a DIFFERENT RSA key (simulating another instance)
        var foreignToken = GenerateForeignInstanceToken(userId: 1);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/@me/servers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", foreignToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForeignToken_WsTicketEndpoint_Returns401()
    {
        var foreignToken = GenerateForeignInstanceToken(userId: 1);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/ws-ticket");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", foreignToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForeignTwoFactorToken_Rejected()
    {
        // Generate a 2FA token signed with a foreign key
        var foreignToken = GenerateForeignTwoFactorToken(userId: 1);

        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/2fa/verify", new
        {
            code = "123456",
            twoFactorToken = foreignToken
        });

        // Should fail validation (INVALID_TOKEN, not succeed)
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("INVALID_TOKEN");
    }

    [Fact]
    public async Task NativeToken_ApiEndpoint_Succeeds()
    {
        // Verify that a token from THIS instance works (sanity check)
        var helper = new TestHelper(_fixture);
        var user = await helper.RegisterUserAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/@me/servers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Creates a JWT access token signed with a completely different RSA key,
    /// simulating a token from another xcord instance.
    /// </summary>
    private static string GenerateForeignInstanceToken(long userId)
    {
        using var foreignRsa = RSA.Create(2048);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("admin", "false"),
            new("email_confirmed", "true"),
            new("bot", "false")
        };

        var credentials = new SigningCredentials(
            new RsaSecurityKey(foreignRsa),
            SecurityAlgorithms.RsaSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "xcord-test",
            Audience = "xcord-test-users",
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(tokenDescriptor));
    }

    /// <summary>
    /// Creates a 2FA token signed with a foreign RSA key.
    /// </summary>
    private static string GenerateForeignTwoFactorToken(long userId)
    {
        using var foreignRsa = RSA.Create(2048);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("purpose", "2fa")
        };

        var credentials = new SigningCredentials(
            new RsaSecurityKey(foreignRsa),
            SecurityAlgorithms.RsaSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            Issuer = "xcord-test",
            Audience = "xcord-test-users",
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(tokenDescriptor));
    }
}
