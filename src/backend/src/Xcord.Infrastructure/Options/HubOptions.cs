using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class HubOptions
{
    public const string SectionName = "Hub";

    [Required]
    public bool Enabled { get; set; }

    public string? GatewayApiUrl { get; set; }

    public string? BootstrapToken { get; set; }

    public string? FederationToken { get; set; }

    public string? Origin { get; set; }

    /// <summary>
    /// Pinned trust anchor for hub-issued tokens, PEM-encoded SubjectPublicKeyInfo
    /// (the contents of "-----BEGIN PUBLIC KEY-----" ... "-----END PUBLIC KEY-----").
    /// Captured by the operator at registration time and stored as part of this
    /// instance's hub-trust configuration. Per-instance: each instance pins exactly
    /// one hub. WHY: without pinning, the JWT validator only checks that an "iss"
    /// claim matches a string. An attacker who can serve a token with the configured
    /// issuer string but signed with their own key would bypass authentication. Pinning
    /// to the hub's actual public key ensures only the configured hub can mint hub
    /// tokens for this instance.
    ///
    /// NOTE: the federation registration handshake currently does not return a hub
    /// public key (see XcordHub.Features.Federation.RegisterHandler) and the hub does
    /// not yet issue RS256 SSO/identity-hint tokens (XcordHub.Infrastructure.Services
    /// .JwtService uses HMAC). When that flow lands, the operator (or the registration
    /// response itself) must populate this field. Until then, this instance MUST reject
    /// any hub-issued token because <see cref="HubPublicKey"/> is null. See
    /// <see cref="Xcord.Api.ServiceCollectionExtensions.AddAuth"/> for the rejection
    /// path.
    /// </summary>
    public string? HubPublicKey { get; set; }

    /// <summary>
    /// Expected "iss" claim value on hub-issued tokens. Used to route tokens to the
    /// hub-pinned validator (matching <see cref="HubPublicKey"/>) versus the
    /// instance's own RSA public key. Must be set whenever <see cref="HubPublicKey"/>
    /// is set, otherwise hub tokens cannot be distinguished from local tokens.
    /// </summary>
    public string? HubIssuer { get; set; }
}
