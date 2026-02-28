using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System.Security.Cryptography;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xcord.Entities;
using Xcord.Infrastructure.Services;
using Xcord.Infrastructure.Data;
using Xunit;

namespace Xcord.Tests.Integration.Fixtures;

public class WebAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("xcord_test")
        .WithUsername("xcord_test")
        .WithPassword("xcord_test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private string _rsaPublicKeyBase64 = string.Empty;

    public HttpClient Client { get; private set; } = null!;
    public WebApplicationFactory<Program> Factory => _factory!;
    public string PostgresConnectionString => _postgres.GetConnectionString();
    public string RedisConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] WebAppFixture: starting containers...");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await Task.WhenAll(
            _postgres.StartAsync(cts.Token),
            _redis.StartAsync(cts.Token)
        );
        Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] WebAppFixture: containers ready ({sw.ElapsedMilliseconds}ms)");

        // Pre-create the database schema from the current model (bypasses broken migrations)
        // and pre-generate RSA key pair for JWT signing/validation
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.EnsureCreatedAsync();

            // Generate RSA key pair for JWT
            using var rsa = RSA.Create(2048);
            var privateKeyBase64 = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
            _rsaPublicKeyBase64 = Convert.ToBase64String(rsa.ExportRSAPublicKey());
            var now = DateTimeOffset.UtcNow;

            db.SystemSettings.Add(new SystemSetting
            {
                Key = "RsaPrivateKey",
                Value = privateKeyBase64,
                CreatedAt = now,
                UpdatedAt = now
            });
            db.SystemSettings.Add(new SystemSetting
            {
                Key = "RsaPublicKey",
                Value = _rsaPublicKeyBase64,
                CreatedAt = now,
                UpdatedAt = now
            });

            await db.SaveChangesAsync();
        }
        Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] WebAppFixture: DB schema + RSA ready ({sw.ElapsedMilliseconds}ms)");

        _factory = new TestWebApplicationFactory(PostgresConnectionString, RedisConnectionString, _rsaPublicKeyBase64);

        Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] WebAppFixture: fully initialized ({sw.ElapsedMilliseconds}ms)");

        // Force server creation to surface any startup failures
        _ = _factory.Server;

        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
    }

    public async Task DisposeAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Client?.Dispose();
        if (_factory != null) await _factory.DisposeAsync();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask()
        );
        Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] WebAppFixture: disposed ({sw.ElapsedMilliseconds}ms)");
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new AppDbContext(options);
    }

    public HttpClient CreateClientWithCookies()
    {
        return _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
    }

    /// <summary>
    /// Creates an HttpMessageHandler for SignalR client connections through the test server.
    /// </summary>
    public HttpMessageHandler CreateHandler()
    {
        return _factory!.Server.CreateHandler();
    }

    /// <summary>
    /// Returns the base address of the test server for SignalR hub connections.
    /// </summary>
    public Uri ServerBaseAddress => _factory!.Server.BaseAddress;
}

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _redisConnectionString;
    private readonly string _rsaPublicKeyBase64;

    public TestWebApplicationFactory(string postgresCs, string redisCs, string rsaPublicKeyBase64)
    {
        _postgresConnectionString = postgresCs;
        _redisConnectionString = redisCs;
        _rsaPublicKeyBase64 = rsaPublicKeyBase64;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ConnectionString", _postgresConnectionString);
        builder.UseSetting("Redis:ConnectionString", _redisConnectionString);
        builder.UseSetting("Redis:ChannelPrefix", "xcord-test");
        builder.UseSetting("Jwt:Issuer", "xcord-test");
        builder.UseSetting("Jwt:Audience", "xcord-test-users");
        builder.UseSetting("Jwt:AccessTokenExpirationMinutes", "15");
        builder.UseSetting("Storage:Endpoint", "http://localhost:9000");
        builder.UseSetting("Storage:AccessKey", "test-access-key");
        builder.UseSetting("Storage:SecretKey", "test-secret-key");
        builder.UseSetting("Storage:Bucket", "test-bucket");
        builder.UseSetting("LiveKit:ApiKey", "test-key");
        builder.UseSetting("LiveKit:ApiSecret", "test-secret-that-is-long-enough-for-hmac");
        builder.UseSetting("LiveKit:Host", "ws://localhost:7880");
        builder.UseSetting("Instance:Domain", "test.xcord.local");
        builder.UseSetting("Instance:Name", "Test Instance");
        builder.UseSetting("Snowflake:WorkerId", "1");
        builder.UseSetting("Encryption:EncryptionKey", "test-encryption-key-for-integration-tests");
        builder.UseSetting("RateLimiting:MaxRequests", "1000");
        builder.UseSetting("RateLimiting:WindowSeconds", "1");
        builder.UseSetting("RateLimiting:AuthRegisterPermitLimit", "10000");
        builder.UseSetting("RateLimiting:AuthForgotPasswordPermitLimit", "10000");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");
        builder.UseSetting("Outbox:PollingIntervalSeconds", "60");
        builder.UseSetting("Outbox:BatchSize", "100");
        builder.UseSetting("Outbox:CleanupIntervalMinutes", "60");
        builder.UseSetting("Outbox:RetentionMinutes", "1440");
        builder.UseSetting("Hub:Enabled", "false");
        builder.UseSetting("Hub:Url", "http://localhost");
        builder.UseSetting("Gif:Provider", "none");
        builder.UseSetting("Gif:ApiKey", "");
        builder.UseSetting("Email:SmtpHost", "localhost");
        builder.UseSetting("Email:SmtpPort", "25");
        builder.UseSetting("Email:FromAddress", "test@xcord.local");
        builder.UseSetting("Email:FromName", "Xcord Test");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("InternalApi:Key", "test-internal-api-key-for-integration");
        builder.UseSetting("Federation:RequireSignatureVerification", "false");
        builder.UseSetting("Tier:MaxStorageMb", "50");

        builder.ConfigureServices(services =>
        {
            // Replace the real S3StorageService with a stub that never touches S3.
            // ExistsAsync returns false (file not found) so ConfirmUpload returns 400
            // instead of 500 from a network error, enabling precise business-rule assertions.
            var storageDescriptors = services
                .Where(d => d.ServiceType == typeof(IStorageService))
                .ToList();
            foreach (var d in storageDescriptors) services.Remove(d);
            services.AddSingleton<IStorageService, NullStorageService>();

            // Override DbContext to suppress PendingModelChangesWarning
            var dbDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null) services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(_postgresConnectionString);
                options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            });

            // Pre-load RsaKeySingleton with the test RSA public key
            var rsaDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(RsaKeySingleton));
            if (rsaDescriptor != null) services.Remove(rsaDescriptor);

            var rsaSingleton = new RsaKeySingleton();
            rsaSingleton.LoadPublicKey(_rsaPublicKeyBase64);
            services.AddSingleton(rsaSingleton);

            // Configure JWT validation with the test RSA key
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.TokenValidationParameters.IssuerSigningKey = rsaSingleton.GetPublicKey();
                });

            // Pre-load EncryptionKeyHolder with the test encryption key
            var encKeyDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(EncryptionKeyHolder));
            if (encKeyDescriptor != null) services.Remove(encKeyDescriptor);

            var encKeyHolder = new EncryptionKeyHolder();
            encKeyHolder.SetKey("test-encryption-key-for-integration-tests");
            services.AddSingleton(encKeyHolder);

            // Remove all hosted services to prevent background tasks from racing
            // the test server startup
            var hostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var descriptor in hostedServices)
                services.Remove(descriptor);
        });
    }
}

/// <summary>
/// A no-op IStorageService used in integration tests so that ConfirmUpload returns 400
/// (file not uploaded) rather than 500 (S3 connection failure) when the file has not
/// been PUT to storage. All operations are safe no-ops; ExistsAsync always returns false.
/// </summary>
public sealed class NullStorageService : IStorageService
{
    public Task<string> GenerateUploadUrlAsync(string key, string contentType, long maxSize, TimeSpan expiry)
        => Task.FromResult($"/api/v1/uploads/{key}/data");

    public Task<string> GenerateDownloadUrlAsync(string key, TimeSpan expiry)
        => Task.FromResult($"/api/v1/attachments/{key}/download");

    public Task DeleteAsync(string key) => Task.CompletedTask;

    /// <summary>Always returns false — no file has been uploaded to storage.</summary>
    public Task<bool> ExistsAsync(string key) => Task.FromResult(false);

    public Task UploadAsync(string key, byte[] data, string contentType) => Task.CompletedTask;

    public Task<byte[]> DownloadAsync(string key) => Task.FromResult(Array.Empty<byte>());
}

[CollectionDefinition("WebApp")]
public class WebAppCollection : ICollectionFixture<WebAppFixture> { }
