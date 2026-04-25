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
        var adminId = await SeedAdminAsync(db, scope);

        // Seed dev test users if configured
        var devUserIds = await SeedDevUsersAsync(db, scope);

        // Seed default server if no servers exist yet
        await SeedDefaultServerAsync(db, scope, adminId, devUserIds);
    }

    private static async Task<long?> SeedAdminAsync(AppDbContext db, IServiceScope scope)
    {
        var adminOptions = scope.ServiceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;

        var hasPassword = !string.IsNullOrWhiteSpace(adminOptions.Password);
        var hasPasswordHash = !string.IsNullOrWhiteSpace(adminOptions.PasswordHash);

        if (string.IsNullOrWhiteSpace(adminOptions.Email) ||
            string.IsNullOrWhiteSpace(adminOptions.Username) ||
            (!hasPassword && !hasPasswordHash))
        {
            return null; // No admin config, skip seeding
        }

        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var snowflakeGenerator = scope.ServiceProvider.GetRequiredService<SnowflakeIdGenerator>();

        // Check if admin already exists by email hash
        var emailHash = encryptionService.ComputeHmac(adminOptions.Email.ToLowerInvariant());
        var existingAdmin = await db.Users
            .Where(u => u.EmailHash == emailHash)
            .Select(u => (long?)u.Id)
            .FirstOrDefaultAsync();

        if (existingAdmin.HasValue)
        {
            return existingAdmin.Value; // Admin already exists - return existing ID
        }

        var now = DateTimeOffset.UtcNow;
        var passwordHash = hasPasswordHash
            ? adminOptions.PasswordHash
            : await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(adminOptions.Password, 12));
        var displayName = !string.IsNullOrWhiteSpace(adminOptions.DisplayName)
            ? adminOptions.DisplayName
            : adminOptions.Username;

        var admin = new User
        {
            Id = snowflakeGenerator.NextId(),
            Username = adminOptions.Username,
            DisplayName = displayName,
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

        return admin.Id;
    }

    private static async Task<List<long>> SeedDevUsersAsync(AppDbContext db, IServiceScope scope)
    {
        var devUsersOptions = scope.ServiceProvider.GetRequiredService<IOptions<DevUsersOptions>>().Value;
        var newUserIds = new List<long>();

        if (devUsersOptions.Users.Count == 0)
            return newUserIds;

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
            var userId = snowflakeGenerator.NextId();

            db.Users.Add(new User
            {
                Id = userId,
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

            newUserIds.Add(userId);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
            Log.Information("Dev test users seeded");
        }

        return newUserIds;
    }

    private static async Task SeedDefaultServerAsync(
        AppDbContext db, IServiceScope scope, long? adminId, List<long> devUserIds)
    {
        if (adminId == null)
            return;

        // Only create a default server if none exist yet
        if (await db.Servers.AnyAsync())
            return;

        var instanceOptions = scope.ServiceProvider.GetRequiredService<IOptions<InstanceOptions>>().Value;
        var snowflakeGenerator = scope.ServiceProvider.GetRequiredService<SnowflakeIdGenerator>();

        var serverName = !string.IsNullOrWhiteSpace(instanceOptions.Name)
            ? instanceOptions.Name
            : "Xcord";

        var now = DateTimeOffset.UtcNow;
        var serverId = snowflakeGenerator.NextId();

        var server = new Server
        {
            Id = serverId,
            Name = serverName,
            OwnerId = adminId.Value,
            MemberCount = 1,
            CreatedAt = now
        };

        db.Servers.Add(server);

        // Admin is the first member
        db.ServerMembers.Add(new ServerMember
        {
            UserId = adminId.Value,
            ServerId = serverId,
            JoinedAt = now
        });

        // @everyone group
        var everyoneGroupId = snowflakeGenerator.NextId();
        db.Groups.Add(new Group
        {
            Id = everyoneGroupId,
            ServerId = serverId,
            Name = "@everyone",
            Color = null,
            Roles = (long)(Role.ViewChannels | Role.SendMessages |
                           Role.EmbedLinks | Role.AttachFiles |
                           Role.ReadMessageHistory | Role.AddReactions |
                           Role.Connect | Role.Speak |
                           Role.CreatePublicThreads | Role.SendMessagesInThreads),
            Position = 0,
            IsEveryone = true,
            CreatedAt = now
        });

        // Member group
        var memberGroupRoles = (long)(Role.ViewChannels | Role.SendMessages | Role.EmbedLinks |
                                      Role.AttachFiles | Role.ReadMessageHistory | Role.AddReactions |
                                      Role.Connect | Role.Speak | Role.CreatePublicThreads |
                                      Role.SendMessagesInThreads | Role.UseExternalEmojis |
                                      Role.ChangeNickname | Role.Video);
        db.Groups.Add(new Group
        {
            Id = snowflakeGenerator.NextId(),
            ServerId = serverId,
            Name = "Member",
            Roles = memberGroupRoles,
            Position = 1,
            CreatedAt = now
        });

        // Moderator group
        db.Groups.Add(new Group
        {
            Id = snowflakeGenerator.NextId(),
            ServerId = serverId,
            Name = "Moderator",
            Color = "#e06a8a",
            Roles = memberGroupRoles | (long)(Role.ManageMessages | Role.KickMembers |
                                              Role.BanMembers | Role.TimeoutMembers |
                                              Role.ManageEmojis | Role.ManageStickers |
                                              Role.ManageNicknames),
            Position = 2,
            CreatedAt = now
        });

        // Bot group
        db.Groups.Add(new Group
        {
            Id = snowflakeGenerator.NextId(),
            ServerId = serverId,
            Name = "Bot",
            Color = "#7289da",
            Roles = (long)(Role.SendMessages | Role.EmbedLinks | Role.AttachFiles |
                           Role.ReadMessageHistory | Role.AddReactions |
                           Role.Connect | Role.Speak),
            Position = 3,
            CreatedAt = now
        });

        // "General" category
        var generalCategoryId = snowflakeGenerator.NextId();
        db.Categories.Add(new Category
        {
            Id = generalCategoryId,
            ServerId = serverId,
            Name = "General",
            Position = 0,
            CreatedAt = now
        });

        // Conversation for the general channel
        var generalConversationId = snowflakeGenerator.NextId();
        db.Conversations.Add(new Conversation
        {
            Id = generalConversationId,
            Type = ConversationType.Channel
        });

        // #general text channel
        var generalChannelId = snowflakeGenerator.NextId();
        db.Channels.Add(new Channel
        {
            Id = generalChannelId,
            ConversationId = generalConversationId,
            ServerId = serverId,
            CategoryId = generalCategoryId,
            Name = "general",
            Type = ChannelType.Text,
            Capabilities = ChannelCapability.Chat,
            Position = 0,
            IsNsfw = false,
            RequireTag = false,
            CreatedAt = now
        });

        // ReadState for the admin
        db.ReadStates.Add(new ReadState
        {
            UserId = adminId.Value,
            ConversationId = generalConversationId,
            UnreadCount = 0,
            MentionCount = 0
        });

        // Add dev users as members (with their own ReadStates)
        foreach (var devUserId in devUserIds)
        {
            db.ServerMembers.Add(new ServerMember
            {
                UserId = devUserId,
                ServerId = serverId,
                JoinedAt = now
            });

            db.ReadStates.Add(new ReadState
            {
                UserId = devUserId,
                ConversationId = generalConversationId,
                UnreadCount = 0,
                MentionCount = 0
            });
        }

        // Update member count to include dev users
        server.MemberCount = 1 + devUserIds.Count;

        // First SaveChanges - persists server, channel, groups, members, conversations
        await db.SaveChangesAsync();

        // Second SaveChanges - set SystemChannelId (avoids circular FK: servers.SystemChannelId -> channels.Id -> servers.Id)
        server.SystemChannelId = generalChannelId;
        await db.SaveChangesAsync();

        Log.Information("Default server '{ServerName}' seeded with #general channel", serverName);
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

        // Production guard: refuse to start without KEK unless explicitly opted out.
        // This must run BEFORE any DB write or DEK generation so we cannot accidentally
        // persist a plaintext DEK in production.
        if (kek == null && app.Environment.IsProduction())
        {
            var allowPlaintext = config.GetValue<bool>("Encryption:AllowPlaintextDek", false);
            if (!allowPlaintext)
            {
                throw new InvalidOperationException(
                    "Production environment requires a configured KEK (Encryption:Kek setting). " +
                    "Plaintext DEK is not allowed. " +
                    "Provide a KEK via /run/secrets/xcord-kek or Encryption:Kek config. " +
                    "To accept the risk of plaintext DEK storage, set Encryption:AllowPlaintextDek=true.");
            }
        }

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
        // 1. If the new keyring table already has rows, load them all.
        var existingKeys = await db.EncryptedDataKeys
            .OrderBy(k => k.Version)
            .ToListAsync();

        if (existingKeys.Count > 0)
        {
            foreach (var keyRow in existingKeys)
            {
                var dekBytes = KeyWrappingService.UnwrapDek(keyRow.WrappedKey, kek);
                encKeyHolder.AddKey(
                    version: (byte)keyRow.Version,
                    keyMaterial: Convert.ToBase64String(dekBytes),
                    isActive: keyRow.IsActive);
            }
            Log.Information(
                "Loaded {Count} encryption key version(s) from encrypted_data_keys; active version is {Active}",
                existingKeys.Count, encKeyHolder.ActiveVersion);
            return;
        }

        // 2. Migration paths from the legacy single-row SystemSettings storage.
        //    Each branch produces version 1 in the new keyring AND removes the
        //    legacy row so subsequent boots take the keyring path above.
        byte[] seedDek;
        string seedSource;

        if (wrappedDbKey != null)
        {
            seedDek = KeyWrappingService.UnwrapDek(Convert.FromBase64String(wrappedDbKey.Value), kek);
            db.SystemSettings.Remove(wrappedDbKey);
            seedSource = "WrappedEncryptionKey";
        }
        else if (plaintextDbKey != null)
        {
            seedDek = Convert.FromBase64String(plaintextDbKey.Value);
            db.SystemSettings.Remove(plaintextDbKey);
            seedSource = "EncryptionKey (plaintext, will be envelope-encrypted)";
        }
        else if (!string.IsNullOrEmpty(configKey))
        {
            seedDek = Convert.FromBase64String(configKey);
            seedSource = "Encryption:EncryptionKey config";
        }
        else
        {
            seedDek = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            seedSource = "newly generated";
        }

        var wrappedSeed = KeyWrappingService.WrapDek(seedDek, kek);
        db.EncryptedDataKeys.Add(new EncryptedDataKey
        {
            Version = 1,
            WrappedKey = wrappedSeed,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        encKeyHolder.AddKey(version: 1, keyMaterial: Convert.ToBase64String(seedDek), isActive: true);
        Log.Information("Encryption keyring initialized at version 1 (source: {Source})", seedSource);
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

        if (await db.EncryptedDataKeys.AnyAsync())
        {
            throw new InvalidOperationException(
                "Database contains wrapped DEKs in encrypted_data_keys but no KEK is configured. " +
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
