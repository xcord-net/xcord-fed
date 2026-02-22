using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xcord.Infrastructure.Data;
using Xunit;

namespace Xcord.Tests.Integration.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("xcord_test")
        .WithUsername("xcord_test")
        .WithPassword("xcord_test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] PostgresFixture: starting container...");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _container.StartAsync(cts.Token);
        Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] PostgresFixture: ready ({sw.ElapsedMilliseconds}ms)");
    }

    public async Task DisposeAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _container.DisposeAsync();
        Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] PostgresFixture: disposed ({sw.ElapsedMilliseconds}ms)");
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public AppDbContext CreateFreshDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        return context;
    }
}
