using FluentAssertions;
using StackExchange.Redis;
using Xcord.Tests.Integration.Fixtures;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class RedisOperationsTests
{
    private readonly IntegrationFixture _fixture;

    public RedisOperationsTests(IntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CanSetAndGetValue()
    {
        using var redis = _fixture.CreateRedisConnection();
        var db = redis.GetDatabase();

        await db.StringSetAsync("test:key", "hello");
        var value = await db.StringGetAsync("test:key");

        value.ToString().Should().Be("hello");
    }

    [Fact]
    public async Task KeyExpirationWorks()
    {
        using var redis = _fixture.CreateRedisConnection();
        var db = redis.GetDatabase();

        await db.StringSetAsync("test:expiring", "temporary", TimeSpan.FromSeconds(2));
        var valueBefore = await db.StringGetAsync("test:expiring");
        valueBefore.HasValue.Should().BeTrue();

        await Task.Delay(3000);
        var valueAfter = await db.StringGetAsync("test:expiring");
        valueAfter.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task SortedSetOperationsWork()
    {
        using var redis = _fixture.CreateRedisConnection();
        var db = redis.GetDatabase();

        var key = "test:sorted";
        await db.SortedSetAddAsync(key, "a", 1);
        await db.SortedSetAddAsync(key, "b", 2);
        await db.SortedSetAddAsync(key, "c", 3);

        var length = await db.SortedSetLengthAsync(key);
        length.Should().Be(3);

        await db.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, 2);
        var remaining = await db.SortedSetLengthAsync(key);
        remaining.Should().Be(1);
    }
}
