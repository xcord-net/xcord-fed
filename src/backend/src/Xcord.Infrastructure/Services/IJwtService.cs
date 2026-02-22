using Xcord;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for generating and validating JWT access tokens.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generates a JWT access token for the specified user.
    /// </summary>
    string GenerateAccessToken(long userId, bool isAdmin, bool emailConfirmed, bool isBot);

    /// <summary>
    /// Generates a short-lived RSA-signed token proving the user passed password auth,
    /// used as proof during 2FA verification.
    /// </summary>
    string GenerateTwoFactorToken(long userId);

    /// <summary>
    /// Validates a 2FA token's signature, issuer, audience, lifetime, and purpose claim.
    /// Returns the userId on success.
    /// </summary>
    Result<long> ValidateTwoFactorToken(string token);

    /// <summary>
    /// Ensures the RSA key pair exists in the database.
    /// Generates and stores a new key pair on first boot if needed.
    /// </summary>
    Task EnsureRsaKeyPairAsync(CancellationToken cancellationToken = default);
}
