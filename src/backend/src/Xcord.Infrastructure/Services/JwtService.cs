using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for generating and validating JWT access tokens using RS256 asymmetric signing.
/// RSA private key is encrypted at rest using the instance DEK via IEncryptionService.
/// </summary>
public sealed class JwtService : IJwtService
{
    private const string RsaPrivateKeySettingKey = "RsaPrivateKey";
    private const string EncryptedRsaPrivateKeySettingKey = "EncryptedRsaPrivateKey";
    private const string RsaPublicKeySettingKey = "RsaPublicKey";
    private const string TwoFactorPurpose = "2fa";

    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly RsaKeySingleton _rsaKeySingleton;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<JwtService> _logger;
    private RSA? _rsa;

    public JwtService(
        AppDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        RsaKeySingleton rsaKeySingleton,
        IEncryptionService encryptionService,
        ILogger<JwtService> logger)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _rsaKeySingleton = rsaKeySingleton;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the RSA key pair exists in the database.
    /// Private key is always stored encrypted with the instance DEK.
    /// Handles migration from plaintext storage.
    /// </summary>
    public async Task EnsureRsaKeyPairAsync(CancellationToken cancellationToken = default)
    {
        var encryptedKeySetting = await _dbContext.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == EncryptedRsaPrivateKeySettingKey, cancellationToken);

        if (encryptedKeySetting != null)
        {
            // Already encrypted — nothing to do
            return;
        }

        var plaintextKeySetting = await _dbContext.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == RsaPrivateKeySettingKey, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (plaintextKeySetting != null)
        {
            // Migrate: encrypt existing plaintext private key, delete plaintext entry
            var encryptedBytes = _encryptionService.Encrypt(plaintextKeySetting.Value);
            _dbContext.SystemSettings.Add(new SystemSetting
            {
                Key = EncryptedRsaPrivateKeySettingKey,
                Value = Convert.ToBase64String(encryptedBytes),
                CreatedAt = now,
                UpdatedAt = now
            });
            _dbContext.SystemSettings.Remove(plaintextKeySetting);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Migrated RSA private key from plaintext to encrypted storage");
        }
        else
        {
            // First boot: generate new RSA key pair, encrypt private key
            using var rsa = RSA.Create(2048);
            var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
            var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());

            var encryptedBytes = _encryptionService.Encrypt(privateKey);

            _dbContext.SystemSettings.Add(new SystemSetting
            {
                Key = EncryptedRsaPrivateKeySettingKey,
                Value = Convert.ToBase64String(encryptedBytes),
                CreatedAt = now,
                UpdatedAt = now
            });

            _dbContext.SystemSettings.Add(new SystemSetting
            {
                Key = RsaPublicKeySettingKey,
                Value = publicKey,
                CreatedAt = now,
                UpdatedAt = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Generated new RSA key pair with encrypted private key");
        }
    }

    /// <summary>
    /// Generates a JWT access token for the specified user.
    /// </summary>
    public string GenerateAccessToken(long userId, bool isAdmin, bool emailConfirmed, bool isBot)
    {
        var rsa = GetRsa();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("admin", isAdmin.ToString().ToLower()),
            new("email_confirmed", emailConfirmed.ToString().ToLower()),
            new("bot", isBot.ToString().ToLower())
        };

        var credentials = new SigningCredentials(
            new RsaSecurityKey(rsa),
            SecurityAlgorithms.RsaSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Generates a short-lived RSA-signed token proving the user passed password auth.
    /// </summary>
    public string GenerateTwoFactorToken(long userId)
    {
        var rsa = GetRsa();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("purpose", TwoFactorPurpose)
        };

        var credentials = new SigningCredentials(
            new RsaSecurityKey(rsa),
            SecurityAlgorithms.RsaSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Validates a 2FA token's signature, issuer, audience, lifetime, and purpose claim.
    /// </summary>
    public Result<long> ValidateTwoFactorToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _rsaKeySingleton.GetPublicKey(),
                ValidateIssuer = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = tokenHandler.ValidateToken(token, validationParams, out _);

            var purposeClaim = principal.FindFirst("purpose")?.Value;
            if (purposeClaim != TwoFactorPurpose)
            {
                return Error.Validation("INVALID_TOKEN", "Invalid two-factor token");
            }

            var subClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (subClaim == null || !long.TryParse(subClaim, out var userId))
            {
                return Error.Validation("INVALID_TOKEN", "Invalid two-factor token");
            }

            return userId;
        }
        catch
        {
            return Error.Validation("INVALID_TOKEN", "Invalid two-factor token");
        }
    }

    /// <summary>
    /// Loads the RSA private key from the database, decrypting it from encrypted storage.
    /// </summary>
    private RSA GetRsa()
    {
        if (_rsa != null)
        {
            return _rsa;
        }

        // Try encrypted key first (normal path)
        var encryptedKeySetting = _dbContext.SystemSettings
            .FirstOrDefault(s => s.Key == EncryptedRsaPrivateKeySettingKey);

        if (encryptedKeySetting != null)
        {
            var encryptedBytes = Convert.FromBase64String(encryptedKeySetting.Value);
            var privateKeyBase64 = _encryptionService.Decrypt(encryptedBytes);
            var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
            _rsa = RSA.Create();
            _rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
            return _rsa;
        }

        throw new InvalidOperationException("RSA key pair not found in database. Call EnsureRsaKeyPairAsync first.");
    }
}
