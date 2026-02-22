using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Xcord.Infrastructure.Data;
using Xunit;

namespace Xcord.Tests.Integration.Fixtures;

public class IntegrationFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    private readonly RedisFixture _redis = new();

    public string PostgresConnectionString => _postgres.ConnectionString;
    public string RedisConnectionString => _redis.ConnectionString;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.InitializeAsync(),
            _redis.InitializeAsync()
        );
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _postgres.DisposeAsync(),
            _redis.DisposeAsync()
        );
    }

    public AppDbContext CreateDbContext()
    {
        return _postgres.CreateDbContext();
    }

    public AppDbContext CreateFreshDbContext()
    {
        return _postgres.CreateFreshDbContext();
    }

    public IConnectionMultiplexer CreateRedisConnection()
    {
        return _redis.CreateConnection();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationFixture> { }
