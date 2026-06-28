using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Tests.Integration.Fixtures;
using Xcord;
using Xunit;
using System.Text;
using System.Security.Cryptography;

namespace Xcord.Tests.Integration;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class SnowflakePersistenceTests
{
    private readonly IntegrationFixture _fixture;
    private readonly SnowflakeIdGenerator _snowflake = new(1);

    public SnowflakePersistenceTests(IntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    private static byte[] EmailToBytes(string email) => Encoding.UTF8.GetBytes(email);
    private static byte[] EmailToHash(string email) => SHA256.HashData(Encoding.UTF8.GetBytes(email));

    [Fact]
    public async Task SnowflakeIdPersistsAsInt64()
    {
        await using var db = await _fixture.CreateFreshDbContextAsync();

        var id = _snowflake.NextId();
        var email = "snowflake@test.com";
        var user = new User
        {
            Id = id,
            Username = "snowflaketest",
            DisplayName = "Snowflake Test",
            Email = EmailToBytes(email),
            EmailHash = EmailToHash(email),
            PasswordHash = "$2a$11$abcdefghijklmnopqrstuuABCDEFGHIJKLMNOPQRSTUVWXYZ012",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var retrieved = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(id);
    }

    [Fact]
    public void SnowflakeIdsAreMonotonicallyIncreasing()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => _snowflake.NextId()).ToList();
        ids.Should().BeInAscendingOrder();
    }
}
