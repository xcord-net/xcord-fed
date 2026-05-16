namespace Xcord.Infrastructure.Options;

/// <summary>
/// Shared-secret authentication for internal endpoints called by xcord-hub.
/// Bound from the "InternalApi" configuration section.
/// </summary>
public sealed class InternalAuthOptions
{
    public const string SectionName = "InternalApi";

    /// <summary>
    /// The shared secret expected in the X-Internal-Key request header.
    /// When empty/null, all InternalKey-policy requests are rejected.
    /// </summary>
    public string Key { get; set; } = string.Empty;
}
