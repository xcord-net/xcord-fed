using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Tests.Unit;

/// <summary>
/// The shape of the LiveKit join token.
/// </summary>
/// <remarks>
/// LiveKit refuses a token whose claims are the right values in the wrong JSON
/// types, and says only "invalid authorization token" - so a mistake here is
/// invisible until voice fails in a browser. These assertions are about types,
/// not values: `video` an object rather than a quoted string, `nbf` and `exp`
/// numbers rather than strings.
/// </remarks>
public class LiveKitTokenTests
{
    private static LiveKitService MakeService() => new(
        Options.Create(new LiveKitOptions
        {
            Host = "wss://livekit.example.com",
            ApiKey = "testkey",
            ApiSecret = "testsecrettestsecrettestsecrettestsecret",
        }),
        new HttpClient(),
        NullLogger<LiveKitService>.Instance);

    private static JsonElement PayloadOf(string jwt)
    {
        var payload = jwt.Split('.')[1];
        payload = payload.Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        return JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement;
    }

    private string Token() => MakeService().GenerateToken(
        userId: 12345,
        roomName: "example.com:voice:678",
        canPublish: true,
        canSubscribe: true,
        canPublishData: true,
        canScreenShare: false,
        ttl: TimeSpan.FromMinutes(30));

    [Fact]
    public void VideoGrantIsAnObjectNotAString()
    {
        var video = PayloadOf(Token()).GetProperty("video");

        video.ValueKind.Should().Be(JsonValueKind.Object,
            "LiveKit parses the grant as an object; a quoted string is opaque to it");
        video.GetProperty("room").GetString().Should().Be("example.com:voice:678");
        video.GetProperty("roomJoin").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void TimeClaimsAreNumericDates()
    {
        var payload = PayloadOf(Token());

        payload.GetProperty("nbf").ValueKind.Should().Be(JsonValueKind.Number);
        payload.GetProperty("exp").ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public void IssuerIsTheApiKeyAndAppearsOnce()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(Token());

        token.Claims.Count(c => c.Type == "iss").Should().Be(1);
        token.Issuer.Should().Be("testkey");
    }
}
