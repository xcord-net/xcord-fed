namespace Xcord.Infrastructure.Options;

public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    /// <summary>
    /// Encryption key (Base64-encoded 32 bytes). Optional — if not provided,
    /// the instance generates its own key on first boot and stores it in SystemSettings.
    /// Standalone operators may provide a key via config; hub-provisioned instances
    /// self-generate.
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;
}
