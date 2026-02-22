using System.Security.Cryptography;
using System.Text;

namespace Xcord.Infrastructure.Services;

public static class TokenHelper
{
    public static string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public static string HashToken(string token)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes);
    }
}
