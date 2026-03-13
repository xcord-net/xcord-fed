namespace Xcord.Infrastructure.Options;

public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    /// <summary>
    /// Data-Encryption-Key (Base64-encoded 32 bytes). Optional - if not provided,
    /// the instance generates its own key on first boot and stores it in SystemSettings.
    /// Standalone operators may provide a key via config; hub-provisioned instances
    /// self-generate.
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Key-Encryption-Key (Base64-encoded 32 bytes). Optional - when provided, the DEK
    /// is wrapped (envelope encryption) so the stored key is useless without the KEK.
    /// Takes lower priority than the KekFile path.
    /// </summary>
    public string Kek { get; set; } = string.Empty;

    /// <summary>
    /// Path to a file containing the KEK (Base64-encoded). Defaults to /run/secrets/xcord-kek.
    /// The file path takes priority over the Kek config value.
    /// </summary>
    public string KekFile { get; set; } = "/run/secrets/xcord-kek";
}
