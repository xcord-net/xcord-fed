namespace Xcord.Infrastructure.Services;

/// <summary>
/// Holds the encryption key for deferred initialization.
/// The key is either loaded from config (standalone) or from the DB (hub-provisioned),
/// or generated on first boot if neither exists.
/// </summary>
public sealed class EncryptionKeyHolder
{
    private string? _key;

    public string Key => _key ?? throw new InvalidOperationException("Encryption key not initialized");

    public void SetKey(string key) => _key = key;
}
