using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service implementation for LiveKit voice infrastructure.
/// Generates JWT tokens and manages room participants via LiveKit API.
/// </summary>
public sealed class LiveKitService : ILiveKitService
{
    private readonly LiveKitOptions _options;
    private readonly HttpClient _httpClient;

    public LiveKitService(IOptions<LiveKitOptions> options, HttpClient httpClient)
    {
        _options = options.Value;
        _httpClient = httpClient;
    }

    public string GenerateToken(
        long userId,
        string roomName,
        bool canPublish,
        bool canSubscribe,
        bool canPublishData,
        bool canScreenShare,
        TimeSpan ttl)
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = now.Add(ttl);

        // Build LiveKit video grant
        var videoGrant = new Dictionary<string, object>
        {
            { "room", roomName },
            { "roomJoin", true },
            { "canPublish", canPublish },
            { "canSubscribe", canSubscribe },
            { "canPublishData", canPublishData }
        };

        // Add screen share capability if permitted
        if (canScreenShare)
        {
            videoGrant["canPublishSources"] = new[] { "camera", "microphone", "screen_share" };
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Iss, _options.ApiKey),
            new Claim(JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds().ToString()),
            new Claim(JwtRegisteredClaimNames.Exp, expiry.ToUnixTimeSeconds().ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("video", JsonSerializer.Serialize(videoGrant))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.ApiKey,
            claims: claims,
            expires: expiry.UtcDateTime,
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }

    public async Task RemoveParticipantAsync(string roomName, string participantIdentity)
    {
        // Generate service-level token for LiveKit API
        var serviceToken = GenerateServiceToken();

        var requestBody = new
        {
            room = roomName,
            identity = participantIdentity
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.Host}/twirp/livekit.RoomService/RemoveParticipant")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Add("Authorization", $"Bearer {serviceToken}");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private string GenerateServiceToken()
    {
        // Generate a service-level token with admin permissions
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddMinutes(5);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Iss, _options.ApiKey),
            new Claim(JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds().ToString()),
            new Claim(JwtRegisteredClaimNames.Exp, expiry.ToUnixTimeSeconds().ToString()),
            new Claim("video", JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "roomAdmin", true }
            }))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.ApiKey,
            claims: claims,
            expires: expiry.UtcDateTime,
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }
}
