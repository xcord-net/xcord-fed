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
public class EntityConfigurationTests
{
    private readonly IntegrationFixture _fixture;
    private readonly SnowflakeIdGenerator _snowflake = new(1);

    public EntityConfigurationTests(IntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    private static byte[] EmailToBytes(string email) => Encoding.UTF8.GetBytes(email);
    private static byte[] EmailToHash(string email) => SHA256.HashData(Encoding.UTF8.GetBytes(email));

    [Fact]
    public async Task CanCreateAndRetrieveUser()
    {
        using var db = _fixture.CreateFreshDbContext();

        var email = "test@example.com";
        var user = new User
        {
            Id = _snowflake.NextId(),
            Username = "testuser",
            DisplayName = "Test User",
            Email = EmailToBytes(email),
            EmailHash = EmailToHash(email),
            PasswordHash = "$2a$11$abcdefghijklmnopqrstuuABCDEFGHIJKLMNOPQRSTUVWXYZ012",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var retrieved = await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Username.Should().Be("testuser");
        retrieved.Email.Should().BeEquivalentTo(EmailToBytes(email));
    }

    [Fact]
    public async Task ServerMemberCascadeDeletesOnServerRemoval()
    {
        using var db = _fixture.CreateFreshDbContext();

        var userId = _snowflake.NextId();
        var email = "owner@test.com";
        var user = new User
        {
            Id = userId,
            Username = "owner",
            DisplayName = "Owner",
            Email = EmailToBytes(email),
            EmailHash = EmailToHash(email),
            PasswordHash = "$2a$11$abcdefghijklmnopqrstuuABCDEFGHIJKLMNOPQRSTUVWXYZ012",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);

        var serverId = _snowflake.NextId();
        var server = new Server
        {
            Id = serverId,
            Name = "Test Server",
            OwnerId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Servers.Add(server);

        var member = new ServerMember
        {
            UserId = userId,
            ServerId = serverId,
            JoinedAt = DateTimeOffset.UtcNow
        };
        db.ServerMembers.Add(member);

        await db.SaveChangesAsync();

        // Remove server
        db.Servers.Remove(server);
        await db.SaveChangesAsync();

        var memberExists = await db.ServerMembers.AnyAsync(m => m.ServerId == serverId);
        memberExists.Should().BeFalse();
    }

    [Fact]
    public async Task SoftDeleteFilterExcludesDeletedEntities()
    {
        using var db = _fixture.CreateFreshDbContext();

        var userId = _snowflake.NextId();
        var email = "deleted@test.com";
        var user = new User
        {
            Id = userId,
            Username = "softdeleted",
            DisplayName = "Soft Deleted",
            Email = EmailToBytes(email),
            EmailHash = EmailToHash(email),
            PasswordHash = "$2a$11$abcdefghijklmnopqrstuuABCDEFGHIJKLMNOPQRSTUVWXYZ012",
            CreatedAt = DateTimeOffset.UtcNow,
            DeletedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Default query should exclude soft-deleted
        var found = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        found.Should().BeNull("soft-delete filter should exclude entities with DeletedAt set");
    }
}
