using System.Collections.Concurrent;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Holds the active and historical Data Encryption Keys (DEKs) for transparent
/// multi-key decryption with periodic rotation.
///
/// New ciphertext is encrypted under the active version. Existing ciphertext
/// continues to decrypt with whichever version is encoded in its leading byte.
/// HMAC blind-index and cursor signing keys are derived from the original
/// version (1) and remain stable across rotations so blind-index lookups and
/// in-flight signed cursors keep working.
///
/// Versions are stored as a single byte (1..255). Version 0 is reserved as
/// a sentinel for legacy AES-CBC ciphertext (which has no version prefix).
/// </summary>
public sealed class EncryptionKeyHolder
{
    private readonly ConcurrentDictionary<byte, string> _keysByVersion = new();

    // int rather than byte so Volatile.Read/Write apply: rotation happens on a
    // different thread than encryption, and a stale read must not outlive the
    // rotation indefinitely. (A briefly-stale version is harmless - the old key
    // stays registered and decryption is version-tagged.)
    private int _activeVersion;

    /// <summary>
    /// The version stamped onto ciphertext produced by Encrypt(). Defaults to 1
    /// once the first key is registered.
    /// </summary>
    public byte ActiveVersion
    {
        get
        {
            var version = Volatile.Read(ref _activeVersion);
            return version != 0 ? (byte)version
                : throw new InvalidOperationException("No active encryption key version registered");
        }
    }

    /// <summary>
    /// All registered key versions, in ascending order.
    /// </summary>
    public IReadOnlyList<byte> Versions
    {
        get
        {
            var v = _keysByVersion.Keys.ToList();
            v.Sort();
            return v;
        }
    }

    /// <summary>
    /// True once at least one key has been registered.
    /// </summary>
    public bool IsInitialized => Volatile.Read(ref _activeVersion) != 0;

    /// <summary>
    /// Backwards-compatible accessor returning the active key. Used by the
    /// single-arg <see cref="PgCryptoEncryptionService"/> constructor and by
    /// callers that historically depended on a single key string.
    /// </summary>
    public string Key => GetKey(ActiveVersion);

    /// <summary>
    /// Single-key initialization (legacy entry point). Registers the supplied
    /// material as version 1 and sets it as the active version.
    /// </summary>
    public void SetKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Encryption key cannot be null or empty.", nameof(key));

        AddKey(version: 1, keyMaterial: key, isActive: true);
    }

    /// <summary>
    /// Registers a key version. Pass <paramref name="isActive"/> = true to make
    /// this version the new target for Encrypt() going forward.
    /// </summary>
    public void AddKey(byte version, string keyMaterial, bool isActive)
    {
        if (version == 0)
            throw new ArgumentOutOfRangeException(nameof(version),
                "Version 0 is reserved for legacy ciphertext.");
        if (string.IsNullOrWhiteSpace(keyMaterial))
            throw new ArgumentException("Key material cannot be null or empty.", nameof(keyMaterial));

        _keysByVersion[version] = keyMaterial;
        if (isActive)
            Volatile.Write(ref _activeVersion, version);
    }

    /// <summary>
    /// Sets the active version among already-registered keys.
    /// </summary>
    public void SetActiveVersion(byte version)
    {
        if (!_keysByVersion.ContainsKey(version))
            throw new InvalidOperationException(
                $"Cannot activate unknown key version {version}.");
        Volatile.Write(ref _activeVersion, version);
    }

    /// <summary>
    /// Returns the raw key material for the given version, or throws if missing.
    /// </summary>
    public string GetKey(byte version)
    {
        if (!_keysByVersion.TryGetValue(version, out var key))
            throw new System.Security.Cryptography.CryptographicException(
                $"Unknown key version {version}");
        return key;
    }

    /// <summary>
    /// Tries to fetch key material for a version without throwing.
    /// </summary>
    public bool TryGetKey(byte version, out string keyMaterial)
    {
        if (_keysByVersion.TryGetValue(version, out var key))
        {
            keyMaterial = key;
            return true;
        }
        keyMaterial = string.Empty;
        return false;
    }
}
