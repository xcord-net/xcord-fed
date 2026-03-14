using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xcord.Infrastructure.Data;
using Xunit;

namespace Xcord.Tests.Integration.Fixtures;

/// <summary>
/// Shared infrastructure fixture for integration tests that need raw container access
/// (not the full WebApplicationFactory). Starts one PostgreSQL, one Redis, and one MinIO
/// container for the entire test assembly and creates isolated databases per caller via
/// <see cref="CreateDatabaseAsync"/>.
/// </summary>
public sealed class SharedInfraFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("xcord_shared")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder()
        .WithImage("minio/minio:RELEASE.2024-11-07T00-52-20Z")
        .Build();

    private readonly ConcurrentDictionary<string, string> _createdDatabases = new();

    public string AdminConnectionString => _postgres.GetConnectionString();
    public string RedisConnectionString => _redis.GetConnectionString();
    public string MinioConnectionString => _minio.GetConnectionString();
    public string MinioAccessKey => _minio.GetAccessKey();
    public string MinioSecretKey => _minio.GetSecretKey();

    public async Task InitializeAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] SharedInfraFixture: starting containers...");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await Task.WhenAll(
            _postgres.StartAsync(cts.Token),
            _redis.StartAsync(cts.Token),
            _minio.StartAsync(cts.Token));
        Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] SharedInfraFixture: ready ({sw.ElapsedMilliseconds}ms)");
    }

    public async Task DisposeAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _minio.DisposeAsync().AsTask());
        Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] SharedInfraFixture: disposed ({sw.ElapsedMilliseconds}ms)");
    }

    /// <summary>
    /// Creates a new database on the shared PG container and returns a connection string.
    /// Idempotent: returns cached connection string on repeat calls with the same name.
    /// </summary>
    public async Task<string> CreateDatabaseAsync(string databaseName)
    {
        if (_createdDatabases.TryGetValue(databaseName, out var cached))
            return cached;

        await using var adminConn = new NpgsqlConnection(AdminConnectionString);
        await adminConn.OpenAsync();
        await using var cmd = adminConn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await cmd.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Database = databaseName
        };
        var connectionString = builder.ToString();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        _createdDatabases[databaseName] = connectionString;
        return connectionString;
    }
}

[CollectionDefinition("SharedInfra")]
public class SharedInfraCollection : ICollectionFixture<SharedInfraFixture> { }
