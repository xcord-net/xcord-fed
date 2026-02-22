using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Authentication handler for bot tokens.
/// Validates "Authorization: Bot {raw_token}" header.
/// </summary>
public sealed class BotAuthenticationHandler : AuthenticationHandler<BotAuthenticationOptions>
{
    private const string AuthorizationHeaderName = "Authorization";
    private const string BotSchemePrefix = "Bot ";

    private readonly AppDbContext _dbContext;

    public BotAuthenticationHandler(
        IOptionsMonitor<BotAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext dbContext)
        : base(options, logger, encoder)
    {
        _dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if Authorization header exists
        if (!Request.Headers.TryGetValue(AuthorizationHeaderName, out var authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var authorizationValue = authorizationHeader.ToString();

        // Check if it's a Bot token
        if (!authorizationValue.StartsWith(BotSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        // Extract raw token
        var rawToken = authorizationValue.Substring(BotSchemePrefix.Length).Trim();

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return AuthenticateResult.Fail("Invalid bot token format");
        }

        // Hash the token
        var tokenHash = HashToken(rawToken);

        // Look up bot token in database
        var botToken = await _dbContext.BotTokens
            .Include(bt => bt.User)
            .FirstOrDefaultAsync(bt => bt.TokenHash == tokenHash);

        if (botToken == null)
        {
            return AuthenticateResult.Fail("Invalid bot token");
        }

        // Check if token is revoked
        if (botToken.IsRevoked)
        {
            return AuthenticateResult.Fail("Bot token has been revoked");
        }

        // Check if user is disabled
        if (botToken.User.IsDisabled)
        {
            return AuthenticateResult.Fail("Bot user account is disabled");
        }

        // Update last used timestamp
        botToken.LastUsedAt = DateTimeOffset.UtcNow;
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to update bot token LastUsedAt for token {TokenId}", botToken.Id);
        }

        // Create claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, botToken.UserId.ToString()),
            new("sub", botToken.UserId.ToString()),
            new("bot", "true"),
            new("bot_permissions", botToken.Permissions.ToString()),
            new("admin", botToken.User.IsAdmin.ToString().ToLower()),
            new("email_confirmed", "true") // Bots are always considered confirmed
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// Hashes a raw token using SHA-256.
    /// </summary>
    private static string HashToken(string rawToken)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

/// <summary>
/// Options for bot authentication.
/// </summary>
public sealed class BotAuthenticationOptions : AuthenticationSchemeOptions
{
}
