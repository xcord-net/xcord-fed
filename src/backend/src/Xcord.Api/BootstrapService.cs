using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Api;

public static class BootstrapService
{
    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
        Log.Information("Database migrations applied");

        // Bootstrap encryption key FIRST - RSA key encryption depends on it
        await InitializeEncryptionAsync(db, app, scope);

        // Ensure RSA key pair exists (private key encrypted at rest with DEK)
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        await jwtService.EnsureRsaKeyPairAsync();
        Log.Information("RSA key pair verified");

        // Load RSA public key into singleton for JWT validation
        var rsaKeySingleton = scope.ServiceProvider.GetRequiredService<RsaKeySingleton>();
        var publicKeySetting = db.SystemSettings.FirstOrDefault(s => s.Key == "RsaPublicKey");
        if (publicKeySetting != null)
        {
            rsaKeySingleton.LoadPublicKey(publicKeySetting.Value);
            Log.Information("RSA public key loaded for JWT validation");
        }

        // Configure JWT validation with the loaded RSA key
        var rsaKey = app.Services.GetRequiredService<RsaKeySingleton>();
        var jwtBearerOptions = app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var currentOptions = jwtBearerOptions.Get(JwtBearerDefaults.AuthenticationScheme);
        if (currentOptions != null)
        {
            currentOptions.TokenValidationParameters.IssuerSigningKey = rsaKey.GetPublicKey();
        }

        // Seed admin account if it doesn't exist yet
        await SeedAdminAsync(db, scope);

        // Seed dev test users if configured
        await SeedDevUsersAsync(db, scope);
    }

    private static async Task SeedAdminAsync(AppDbContext db, IServiceScope scope)
    {
        var adminOptions = scope.ServiceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;

        if (string.IsNullOrWhiteSpace(adminOptions.Email) ||
            string.IsNullOrWhiteSpace(adminOptions.Password) ||
            string.IsNullOrWhiteSpace(adminOptions.Username))
        {
            return; // No admin config, skip seeding
        }

        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var snowflakeGenerator = scope.ServiceProvider.GetRequiredService<SnowflakeIdGenerator>();

        // Check if admin already exists by email hash
        var emailHash = encryptionService.ComputeHmac(adminOptions.Email.ToLowerInvariant());
        var adminExists = await db.Users.AnyAsync(u => u.EmailHash == emailHash);

        if (adminExists)
        {
            return; // Admin already exists
        }

        var now = DateTimeOffset.UtcNow;
        var passwordHash = await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(adminOptions.Password, 12));

        var admin = new User
        {
            Id = snowflakeGenerator.NextId(),
            Username = adminOptions.Username,
            DisplayName = adminOptions.Username,
            Email = encryptionService.Encrypt(adminOptions.Email.ToLowerInvariant()),
            EmailHash = emailHash,
            PasswordHash = passwordHash,
            EmailConfirmed = true,
            TwoFactorEnabled = false,
            IsAdmin = true,
            IsBot = false,
            IsDisabled = false,
            CreatedAt = now,
            LastLoginAt = now
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync();
        Log.Information("Admin account seeded: {Username}", adminOptions.Username);
    }

    private static async Task SeedDevUsersAsync(AppDbContext db, IServiceScope scope)
    {
        var devUsersOptions = scope.ServiceProvider.GetRequiredService<IOptions<DevUsersOptions>>().Value;

        if (devUsersOptions.Users.Count == 0)
            return;

        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var snowflakeGenerator = scope.ServiceProvider.GetRequiredService<SnowflakeIdGenerator>();
        var now = DateTimeOffset.UtcNow;

        foreach (var devUser in devUsersOptions.Users)
        {
            if (string.IsNullOrWhiteSpace(devUser.Email) ||
                string.IsNullOrWhiteSpace(devUser.Password) ||
                string.IsNullOrWhiteSpace(devUser.Username))
                continue;

            var emailHash = encryptionService.ComputeHmac(devUser.Email.ToLowerInvariant());
            if (await db.Users.AnyAsync(u => u.EmailHash == emailHash))
                continue;

            var passwordHash = await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(devUser.Password, 12));

            db.Users.Add(new User
            {
                Id = snowflakeGenerator.NextId(),
                Username = devUser.Username,
                DisplayName = devUser.Username,
                Email = encryptionService.Encrypt(devUser.Email.ToLowerInvariant()),
                EmailHash = emailHash,
                PasswordHash = passwordHash,
                EmailConfirmed = true,
                TwoFactorEnabled = false,
                IsAdmin = false,
                IsBot = false,
                IsDisabled = false,
                CreatedAt = now,
                LastLoginAt = now
            });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
            Log.Information("Dev test users seeded");
        }
    }

    private static async Task InitializeEncryptionAsync(AppDbContext db, WebApplication app, IServiceScope scope)
    {
        var encKeyHolder = app.Services.GetRequiredService<EncryptionKeyHolder>();
        var kekProvider = app.Services.GetRequiredService<IKekProvider>();
        var kek = kekProvider.GetKek();

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var configKey = config.GetSection("Encryption:EncryptionKey").Value;
        var wrappedDbKey = db.SystemSettings.FirstOrDefault(s => s.Key == "WrappedEncryptionKey");
        var plaintextDbKey = db.SystemSettings.FirstOrDefault(s => s.Key == "EncryptionKey");

        if (kek != null)
        {
            await InitializeWithKekAsync(db, encKeyHolder, kek, configKey, wrappedDbKey, plaintextDbKey);
        }
        else
        {
            await InitializeWithoutKekAsync(db, encKeyHolder, configKey, wrappedDbKey, plaintextDbKey);
        }
    }

    private static async Task InitializeWithKekAsync(
        AppDbContext db, EncryptionKeyHolder encKeyHolder, byte[] kek,
        string? configKey, SystemSetting? wrappedDbKey, SystemSetting? plaintextDbKey)
    {
        if (wrappedDbKey != null)
        {
            // Normal restart: unwrap DEK from DB
            var wrappedBytes = Convert.FromBase64String(wrappedDbKey.Value);
            var dekBytes = KeyWrappingService.UnwrapDek(wrappedBytes, kek);
            encKeyHolder.SetKey(Convert.ToBase64String(dekBytes));
            Log.Information("Encryption key unwrapped from database using KEK");
        }
        else if (plaintextDbKey != null)
        {
            // Migration: wrap existing plaintext DEK with KEK
            var dekBytes = Convert.FromBase64String(plaintextDbKey.Value);
            var wrappedBytes = KeyWrappingService.WrapDek(dekBytes, kek);
            db.SystemSettings.Add(new SystemSetting
            {
                Key = "WrappedEncryptionKey",
                Value = Convert.ToBase64String(wrappedBytes),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            db.SystemSettings.Remove(plaintextDbKey);
            await db.SaveChangesAsync();
            encKeyHolder.SetKey(plaintextDbKey.Value);
            Log.Information("Migrated plaintext encryption key to envelope encryption");
        }
        else if (!string.IsNullOrEmpty(configKey))
        {
            // Migration: wrap config-provided DEK with KEK
            var dekBytes = Convert.FromBase64String(configKey);
            var wrappedBytes = KeyWrappingService.WrapDek(dekBytes, kek);
            db.SystemSettings.Add(new SystemSetting
            {
                Key = "WrappedEncryptionKey",
                Value = Convert.ToBase64String(wrappedBytes),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            encKeyHolder.SetKey(configKey);
            Log.Information("Wrapped config encryption key with KEK and stored in database");
        }
        else
        {
            // First boot with KEK: generate DEK, wrap it, store wrapped
            var dekBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            var wrappedBytes = KeyWrappingService.WrapDek(dekBytes, kek);
            db.SystemSettings.Add(new SystemSetting
            {
                Key = "WrappedEncryptionKey",
                Value = Convert.ToBase64String(wrappedBytes),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            encKeyHolder.SetKey(Convert.ToBase64String(dekBytes));
            Log.Information("Generated new encryption key (envelope-encrypted) on first boot");
        }
    }

    private static async Task InitializeWithoutKekAsync(
        AppDbContext db, EncryptionKeyHolder encKeyHolder,
        string? configKey, SystemSetting? wrappedDbKey, SystemSetting? plaintextDbKey)
    {
        if (wrappedDbKey != null)
        {
            throw new InvalidOperationException(
                "Database contains a wrapped encryption key (WrappedEncryptionKey) but no KEK is configured. " +
                "Provide the KEK via /run/secrets/xcord-kek or Encryption:Kek config.");
        }

        if (!string.IsNullOrEmpty(configKey))
        {
            if (plaintextDbKey == null)
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = "EncryptionKey",
                    Value = configKey,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }
            encKeyHolder.SetKey(configKey);
            Log.Warning("Encryption key loaded from configuration WITHOUT envelope encryption - configure a KEK for production use");
        }
        else if (plaintextDbKey != null)
        {
            encKeyHolder.SetKey(plaintextDbKey.Value);
            Log.Warning("Encryption key loaded from database WITHOUT envelope encryption - configure a KEK for production use");
        }
        else
        {
            var newKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            db.SystemSettings.Add(new SystemSetting
            {
                Key = "EncryptionKey",
                Value = newKey,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            encKeyHolder.SetKey(newKey);
            Log.Warning("Generated new encryption key WITHOUT envelope encryption - configure a KEK for production use");
        }
    }
}
