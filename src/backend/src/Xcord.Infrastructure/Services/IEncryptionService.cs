namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for encrypting/decrypting sensitive data and computing blind indexes.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext using the instance's encryption key.
    /// </summary>
    /// <param name="plaintext">The plaintext to encrypt.</param>
    /// <returns>Encrypted bytes.</returns>
    byte[] Encrypt(string plaintext);

    /// <summary>
    /// Decrypts ciphertext using the instance's encryption key.
    /// </summary>
    /// <param name="ciphertext">The encrypted bytes.</param>
    /// <returns>Decrypted plaintext.</returns>
    string Decrypt(byte[] ciphertext);

    /// <summary>
    /// Computes HMAC-SHA256 blind index for a value.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <returns>HMAC-SHA256 hash bytes.</returns>
    byte[] ComputeHmac(string value);
}
