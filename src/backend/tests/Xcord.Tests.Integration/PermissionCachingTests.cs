using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Integration tests that verify the Redis cache-aside pattern in PermissionService.
///
/// Three scenarios are tested for both server-level and channel-level permissions:
///   1. After the first permission check, a Redis key is created.
///   2. The second identical check is served from Redis (DB is not re-queried).
///   3. After the cache expires, the DB is queried again.
///
/// "Served from Redis" is verified by pre-loading a specific value into Redis before
/// the HTTP call and confirming the service uses that cached value rather than
/// computing fresh permissions from the DB.
///
/// Cache expiry is simulated by forcing an immediate Redis key expiry (avoiding the real
/// 60-second TTL).
///
/// The Redis key prefix used by the test web app is "xcord-test"
/// (configured in TestWebApplicationFactory as Redis:ChannelPrefix).
/// Key format: {prefix}:perms:server:{userId}:{serverId}
///             {prefix}:perms:channel:{userId}:{channelId}
/// </summary>
[Collection("WebApp")]
public class PermissionCachingTests
{
    // Must match the "Redis:ChannelPrefix" value in TestWebApplicationFactory.
    private const string RedisPrefix = "xcord-test";

    // ManageChannels = 1L << 3 = 8  (from Xcord.Shared/Entities/Permission.cs)
    private const long ManageChannelsPermission = 8L;

    // ViewChannels = 1L << 0 = 1
    private const long ViewChannelsPermission = 1L;

    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public PermissionCachingTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ─────────────────── Server permission cache ───────────────────

    /// <summary>
    /// Verifies that after the first call to an endpoint that checks a server permission,
    /// PermissionService writes a Redis key for that (userId, serverId) pair.
    /// </summary>
    [Fact]
    public async Task ServerPermissionCheck_FirstCall_CreatesRedisKey()
    {
        // Arrange — create owner (who has all permissions) and derive the cache key
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var serverCacheKey = $"{RedisPrefix}:perms:server:{owner.UserId}:{serverId}";

        using var redis = ConnectionMultiplexer.Connect(_fixture.RedisConnectionString);
        var db = redis.GetDatabase();

        // Ensure the key is absent before the call
        await db.KeyDeleteAsync(serverCacheKey);
        (await db.KeyExistsAsync(serverCacheKey)).Should().BeFalse("key must not exist before the first permission check");

        // Act — POST to create a channel; CreateChannelHandler calls
        // permissionService.EnsureServerPermission → GetServerPermissions → caches the result.
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            owner.AccessToken,
            new { name = "cache-test-channel", type = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "owner should be able to create a channel (all permissions granted)");

        // Assert — Redis key must exist with a parseable long value
        var keyExists = await db.KeyExistsAsync(serverCacheKey);
        keyExists.Should().BeTrue("PermissionService must write a cache entry after the first server permission check");

        var cached = await db.StringGetAsync(serverCacheKey);
        cached.HasValue.Should().BeTrue();
        long.TryParse(cached.ToString(), out _).Should().BeTrue("cached value must be a valid long");
    }

    /// <summary>
    /// Verifies that the second call to an endpoint that checks a server permission is
    /// served from Redis and does not re-query the database.
    ///
    /// Strategy: pre-load a specific value (ManageChannels) into Redis for a member who
    /// does NOT have that permission in the database.  The first API call succeeds
    /// (Redis is the authority), proving the service read from cache.
    /// </summary>
    [Fact]
    public async Task ServerPermissionCheck_SecondCall_IsServedFromRedis()
    {
        // Arrange — create server and invite a regular member
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var serverCacheKey = $"{RedisPrefix}:perms:server:{member.UserId}:{serverId}";

        using var redis = ConnectionMultiplexer.Connect(_fixture.RedisConnectionString);
        var db = redis.GetDatabase();

        // Confirm the member does NOT have ManageChannels via the DB
        // (the @everyone role only grants ViewChannels, SendMessages, etc., not ManageChannels).
        var deniedResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "should-fail", type = 0 });
        deniedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "member without ManageChannels must be denied before we plant the cache value");

        // Plant a cache value that grants ManageChannels to the member.
        // A real DB query would still return the @everyone permissions (no ManageChannels).
        await db.StringSetAsync(serverCacheKey, ManageChannelsPermission.ToString(), TimeSpan.FromSeconds(60));

        // Act — same call, same member.  PermissionService finds the key in Redis and
        // returns ManageChannels without querying the DB.
        var cachedResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "cache-granted-channel", type = 0 });

        // Assert — the call succeeded because Redis granted ManageChannels
        cachedResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "PermissionService must serve permissions from the Redis cache, not re-query the DB");
    }

    /// <summary>
    /// Verifies that after the Redis cache entry expires, PermissionService falls back to
    /// querying the database.
    ///
    /// Strategy: pre-load a ManageChannels value into Redis for a member who does NOT have
    /// it in the DB, then force the key to expire immediately.  The next call must return
    /// 403 because the DB (which still has no ManageChannels for this member) is queried.
    /// </summary>
    [Fact]
    public async Task ServerPermissionCheck_AfterCacheExpiry_QueriesDbAgain()
    {
        // Arrange — create server and invite a regular member
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var serverCacheKey = $"{RedisPrefix}:perms:server:{member.UserId}:{serverId}";

        using var redis = ConnectionMultiplexer.Connect(_fixture.RedisConnectionString);
        var db = redis.GetDatabase();

        // Plant the elevated permissions in cache
        await db.StringSetAsync(serverCacheKey, ManageChannelsPermission.ToString(), TimeSpan.FromSeconds(60));

        // Verify the planted key is served (warm cache, same as previous test)
        var warmResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "warm-cache-channel", type = 0 });
        warmResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "the planted ManageChannels value in Redis must be served while the cache is warm");

        // Force cache expiry — avoids waiting the real 60-second TTL
        await db.KeyExpireAsync(serverCacheKey, TimeSpan.FromMilliseconds(1));
        await Task.Delay(50); // Allow Redis to process the expiry

        // Confirm the key is gone
        (await db.KeyExistsAsync(serverCacheKey)).Should().BeFalse("cache key must have expired before we proceed");

        // Act — call again; cache is empty so PermissionService must query the DB.
        // The DB has no ManageChannels for this member → 403.
        var expiredResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "post-expiry-channel", type = 0 });

        // Assert
        expiredResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "after cache expiry, PermissionService must re-query the DB which returns no ManageChannels for this member");

        // After the expiry call, a new cache entry must have been written with the real DB value
        var newCachedValue = await db.StringGetAsync(serverCacheKey);
        newCachedValue.HasValue.Should().BeTrue("PermissionService must re-populate the cache after a DB query");

        // The new cached value must NOT equal the planted ManageChannels value
        var parsedNewValue = long.Parse(newCachedValue.ToString());
        (parsedNewValue & ManageChannelsPermission).Should().Be(0,
            "after the DB re-query, the cached permissions must reflect the real DB value (no ManageChannels)");
    }

    // ─────────────────── Channel permission cache ───────────────────

    /// <summary>
    /// Verifies that after the first call to an endpoint that checks a channel permission,
    /// PermissionService writes a Redis key for that (userId, channelId) pair.
    ///
    /// GetChannelHandler calls permissionService.EnsureChannelPermission(ViewChannels)
    /// which calls GetChannelPermissions and caches the result.
    /// </summary>
    [Fact]
    public async Task ChannelPermissionCheck_FirstCall_CreatesRedisKey()
    {
        // Arrange — create owner, server, and channel
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();

        var channelCacheKey = $"{RedisPrefix}:perms:channel:{owner.UserId}:{channelId}";

        using var redis = ConnectionMultiplexer.Connect(_fixture.RedisConnectionString);
        var db = redis.GetDatabase();

        // Ensure the key is absent before the call
        await db.KeyDeleteAsync(channelCacheKey);
        (await db.KeyExistsAsync(channelCacheKey)).Should().BeFalse("key must not exist before the first permission check");

        // Act — GET /api/v1/channels/{channelId} calls EnsureChannelPermission
        var response = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}", owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "owner should be able to view the channel");

        // Assert — channel cache key must now exist
        var keyExists = await db.KeyExistsAsync(channelCacheKey);
        keyExists.Should().BeTrue("PermissionService must write a cache entry after the first channel permission check");

        var cached = await db.StringGetAsync(channelCacheKey);
        cached.HasValue.Should().BeTrue();
        long.TryParse(cached.ToString(), out _).Should().BeTrue("cached value must be a valid long");
    }

    /// <summary>
    /// Verifies that the second call to a channel-permission-gated endpoint is served from
    /// Redis rather than the database.
    ///
    /// Strategy: pre-load a ViewChannels value into the channel cache for a member who does
    /// NOT have ViewChannels in the DB (we'll strip the @everyone role's ViewChannels via
    /// direct DB update, bypassing the cache-invalidation path).  The call must succeed,
    /// proving the service used the Redis cache.
    /// </summary>
    [Fact]
    public async Task ChannelPermissionCheck_SecondCall_IsServedFromRedis()
    {
        // Arrange — create server and invite a member
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var channelCacheKey = $"{RedisPrefix}:perms:channel:{member.UserId}:{channelId}";
        var serverCacheKey = $"{RedisPrefix}:perms:server:{member.UserId}:{serverId}";

        using var redis = ConnectionMultiplexer.Connect(_fixture.RedisConnectionString);
        var db = redis.GetDatabase();

        // Confirm the member CAN see the channel normally (ViewChannels is in @everyone)
        var normalResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}", member.AccessToken);
        normalResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "member with @everyone ViewChannels should be able to see the channel normally");

        // Strip ViewChannels from the @everyone role directly in the DB to change what
        // a fresh DB query would return.  We bypass the API to avoid cache invalidation.
        await using (var ctx = _fixture.CreateDbContext())
        {
            var everyoneRole = await ctx.Roles
                .FirstOrDefaultAsync(r => r.ServerId == serverId && r.IsEveryone);

            everyoneRole.Should().NotBeNull("@everyone role must exist");

            // Remove ViewChannels from the @everyone permissions
            everyoneRole!.Permissions &= ~ViewChannelsPermission;
            await ctx.SaveChangesAsync();
        }

        // Also delete the server-level cache for this user so GetChannelPermissions
        // would have to re-run the full resolution from DB on a cache miss.
        await db.KeyDeleteAsync(serverCacheKey);

        // The channel cache key was populated by the first successful call above and still holds
        // the original permissions (which include ViewChannels).  Confirm it is still there.
        (await db.KeyExistsAsync(channelCacheKey)).Should().BeTrue("channel cache must still be populated from the first call");

        // Act — second call, same member.  The DB now says no ViewChannels,
        // but the channel cache still holds the original permissions.
        var cachedResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}", member.AccessToken);

        // Assert — still 200, meaning the cached value (ViewChannels) was used
        cachedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "PermissionService must serve the channel permission from Redis cache, not re-query the DB");
    }

    /// <summary>
    /// Verifies that after the Redis channel-permission cache entry expires,
    /// PermissionService falls back to querying the database.
    /// </summary>
    [Fact]
    public async Task ChannelPermissionCheck_AfterCacheExpiry_QueriesDbAgain()
    {
        // Arrange — create server and invite a member
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var channelCacheKey = $"{RedisPrefix}:perms:channel:{member.UserId}:{channelId}";
        var serverCacheKey = $"{RedisPrefix}:perms:server:{member.UserId}:{serverId}";

        using var redis = ConnectionMultiplexer.Connect(_fixture.RedisConnectionString);
        var db = redis.GetDatabase();

        // First call — populates the cache with ViewChannels permissions
        var firstResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}", member.AccessToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await db.KeyExistsAsync(channelCacheKey)).Should().BeTrue("channel cache must be populated after first call");

        // Strip ViewChannels from @everyone in DB (bypass API to avoid cache invalidation)
        await using (var ctx = _fixture.CreateDbContext())
        {
            var everyoneRole = await ctx.Roles
                .FirstOrDefaultAsync(r => r.ServerId == serverId && r.IsEveryone);

            everyoneRole.Should().NotBeNull();
            everyoneRole!.Permissions &= ~ViewChannelsPermission;
            await ctx.SaveChangesAsync();
        }

        // Force both the channel and server cache keys to expire immediately
        await db.KeyExpireAsync(channelCacheKey, TimeSpan.FromMilliseconds(1));
        await db.KeyExpireAsync(serverCacheKey, TimeSpan.FromMilliseconds(1));
        await Task.Delay(50); // Allow Redis to process the expiry

        // Verify both keys are gone
        (await db.KeyExistsAsync(channelCacheKey)).Should().BeFalse("channel cache key must have expired");
        (await db.KeyExistsAsync(serverCacheKey)).Should().BeFalse("server cache key must have expired");

        // Act — call again; cache is empty so PermissionService must query the DB.
        // DB now has no ViewChannels for @everyone → 403.
        var expiredResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}", member.AccessToken);

        // Assert
        expiredResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "after cache expiry, PermissionService must re-query the DB which now returns no ViewChannels for this member");

        // A new channel cache entry must have been written with the real DB value
        var newCachedValue = await db.StringGetAsync(channelCacheKey);
        newCachedValue.HasValue.Should().BeTrue("PermissionService must re-populate the channel cache after a DB query");

        var parsedNewValue = long.Parse(newCachedValue.ToString());
        (parsedNewValue & ViewChannelsPermission).Should().Be(0,
            "the re-populated cache must reflect the updated DB value (no ViewChannels)");
    }
}
