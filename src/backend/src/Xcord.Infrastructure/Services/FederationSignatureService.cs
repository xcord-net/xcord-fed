using System.Security.Cryptography;
using System.Text;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Handles HMAC-SHA256 signing and verification for federation messages.
/// </summary>
public static class FederationSignatureService
{
    private static readonly TimeSpan MaxTimestampSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Signs a request body with HMAC-SHA256 using the shared secret.
    /// Returns the signature header value in the format "t={timestamp},sig={hex}".
    /// </summary>
    public static string Sign(byte[] sharedSecret, string body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{timestamp}.{body}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(sharedSecret);
        var hash = hmac.ComputeHash(payloadBytes);
        var hex = Convert.ToHexStringLower(hash);

        return $"t={timestamp},sig={hex}";
    }

    /// <summary>
    /// Verifies a federation signature header against the request body.
    /// Returns null on success, or an error message on failure.
    /// </summary>
    public static string? Verify(byte[] sharedSecret, string body, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return "Missing signature header";

        // Parse "t={timestamp},sig={hex}"
        long timestamp = 0;
        string? sig = null;

        foreach (var part in signatureHeader.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("t=", StringComparison.Ordinal))
                long.TryParse(trimmed.AsSpan(2), out timestamp);
            else if (trimmed.StartsWith("sig=", StringComparison.Ordinal))
                sig = trimmed[4..];
        }

        if (timestamp == 0 || string.IsNullOrEmpty(sig))
            return "Malformed signature header";

        // Check timestamp freshness
        var signedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        var age = DateTimeOffset.UtcNow - signedAt;
        if (age > MaxTimestampSkew || age < -MaxTimestampSkew)
            return "Stale signature (timestamp outside 5-minute window)";

        // Compute expected HMAC
        var payload = $"{timestamp}.{body}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(sharedSecret);
        var expectedHash = hmac.ComputeHash(payloadBytes);
        var expectedHex = Convert.ToHexStringLower(expectedHash);

        // Constant-time comparison
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHex),
            Encoding.UTF8.GetBytes(sig)))
        {
            return "Invalid signature";
        }

        return null;
    }

    /// <summary>
    /// Generates a cryptographically random shared secret (32 bytes).
    /// </summary>
    public static byte[] GenerateSharedSecret()
    {
        return RandomNumberGenerator.GetBytes(32);
    }
}
