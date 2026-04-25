namespace Xcord.Infrastructure.Services;

/// <summary>
/// Rotates the active Data Encryption Key (DEK). Generates a fresh 32-byte DEK,
/// wraps it under the KEK, persists it to the encrypted_data_keys table as a new
/// version, marks it active, and updates the in-memory key holder so subsequent
/// calls to <see cref="IEncryptionService.Encrypt"/> use the new version.
///
/// Existing ciphertext is NOT re-encrypted; it remains decryptable through the
/// older version's row in encrypted_data_keys. A separate backfill process can
/// be run if cryptographic erasure of an old key is desired.
/// </summary>
public interface IKeyRotationService
{
    /// <summary>
    /// Performs a rotation. Returns the new active version number.
    /// </summary>
    Task<int> RotateDataKeyAsync(CancellationToken cancellationToken = default);
}
