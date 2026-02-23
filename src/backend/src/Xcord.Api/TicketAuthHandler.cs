using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Xcord.Infrastructure.Options;

namespace Xcord.Api;

/// <summary>
/// Authentication handler for WebSocket ticket-based authentication.
/// Allows clients to pass a short-lived ticket in the query string to avoid exposing JWT in URL.
/// </summary>
public class TicketAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _channelPrefix;

    public TicketAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions)
        : base(options, logger, encoder)
    {
        _redis = redis;
        _channelPrefix = redisOptions.Value.ChannelPrefix;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for ticket in query string
        if (!Request.Query.TryGetValue("ticket", out var ticketValue) || string.IsNullOrWhiteSpace(ticketValue))
        {
            // No ticket provided, skip this handler (fall back to JWT)
            return AuthenticateResult.NoResult();
        }

        var ticket = ticketValue.ToString();
        var db = _redis.GetDatabase();
        var ticketKey = $"{_channelPrefix}:wsticket:{ticket}";

        // Retrieve userId from Redis
        var userIdString = await db.StringGetAsync(ticketKey);
        if (!userIdString.HasValue)
        {
            Logger.LogWarning("Invalid or expired WebSocket ticket: {Ticket}", ticket);
            return AuthenticateResult.Fail("Invalid or expired ticket");
        }

        // Delete the ticket immediately (single use)
        await db.KeyDeleteAsync(ticketKey);

        // Parse userId
        if (!long.TryParse((string?)userIdString, out var userId))
        {
            Logger.LogError("Invalid userId format in ticket: {UserId}", userIdString);
            return AuthenticateResult.Fail("Invalid ticket data");
        }

        // Create claims principal
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var authTicket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("Successfully authenticated user {UserId} via WebSocket ticket", userId);

        return AuthenticateResult.Success(authTicket);
    }
}
