using Testcontainers.Redis;
using StackExchange.Redis;
using Xunit;

namespace Xcord.Tests.Integration.Fixtures;

public class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public IConnectionMultiplexer CreateConnection()
    {
        return ConnectionMultiplexer.Connect(ConnectionString);
    }
}
