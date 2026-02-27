namespace Xcord.Infrastructure.Options;

public sealed class FederationOptions
{
    public const string SectionName = "Federation";

    /// <summary>
    /// When true, all inbound federation requests MUST include a valid X-Federation-Signature header.
    /// Set to false only during development while federation key exchange is not yet implemented.
    /// </summary>
    public bool RequireSignatureVerification { get; set; } = true;
}
