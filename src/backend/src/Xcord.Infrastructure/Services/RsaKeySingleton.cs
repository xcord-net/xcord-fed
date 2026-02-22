using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Singleton that holds the loaded RSA key for JWT validation.
/// </summary>
public sealed class RsaKeySingleton
{
    private RsaSecurityKey? _publicKey;

    public void LoadPublicKey(string base64Key)
    {
        var publicKeyBytes = Convert.FromBase64String(base64Key);
        var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(publicKeyBytes, out _);
        _publicKey = new RsaSecurityKey(rsa);
    }

    public RsaSecurityKey GetPublicKey()
    {
        if (_publicKey == null)
        {
            throw new InvalidOperationException("RSA public key has not been loaded");
        }

        return _publicKey;
    }
}
