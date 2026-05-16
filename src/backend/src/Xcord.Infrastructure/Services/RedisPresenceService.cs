using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Entities;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Redis-backed implementation of presence service.
/// Uses hash for user status and sorted sets for per-server tracking.
/// </summary>
public sealed class RedisPresenceService : IPresenceService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _prefix;

    public RedisPresenceService(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions)
    {
        _redis = redis;
        _prefix = redisOptions.Value.ChannelPrefix;
    }

    public async Task SetStatusAsync(long userId, PresenceStatus status)
    {
        var db = _redis.GetDatabase();
        var key = GetUserPresenceKey(userId);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await db.HashSetAsync(key, new HashEntry[]
        {
            new HashEntry("status", status.ToString()),
            new HashEntry("lastSeen", now)
        });
    }

    public async Task<PresenceStatus> GetStatusAsync(long userId)
    {
        var db = _redis.GetDatabase();
        var key = GetUserPresenceKey(userId);
        var statusValue = await db.HashGetAsync(key, "status").ConfigureAwait(false);

        if (statusValue.IsNullOrEmpty)
        {
            return PresenceStatus.Offline;
        }

        return Enum.TryParse<PresenceStatus>(statusValue.ToString(), out var status)
            ? status
            : PresenceStatus.Offline;
    }

    public async Task<DateTime?> GetLastSeenAsync(long userId)
    {
        var db = _redis.GetDatabase();
        var key = GetUserPresenceKey(userId);
        var lastSeenValue = await db.HashGetAsync(key, "lastSeen").ConfigureAwait(false);

        if (lastSeenValue.IsNullOrEmpty)
        {
            return null;
        }

        var unixTime = (long)lastSeenValue;
        return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
    }

    public async Task HeartbeatAsync(long userId, IEnumerable<long> serverIds)
    {
        var db = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Update lastSeen in user presence hash
        var userKey = GetUserPresenceKey(userId);
        await db.HashSetAsync(userKey, "lastSeen", now).ConfigureAwait(false);

        // Update scores in all server sorted sets
        var tasks = serverIds.Select(serverId =>
        {
            var serverKey = GetServerPresenceKey(serverId);
            return db.SortedSetAddAsync(serverKey, userId, now);
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task<Dictionary<long, PresenceStatus>> GetBulkStatusAsync(IEnumerable<long> userIds)
    {
        var db = _redis.GetDatabase();
        var result = new Dictionary<long, PresenceStatus>();

        var tasks = userIds.Select(async userId =>
        {
            var status = await GetStatusAsync(userId).ConfigureAwait(false);
            return (userId, status);
        });

        var statuses = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var (userId, status) in statuses)
        {
            result[userId] = status;
        }

        return result;
    }

    public async Task RemovePresenceAsync(long userId, IEnumerable<long> serverIds)
    {
        var db = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Set status to Offline and update lastSeen
        var userKey = GetUserPresenceKey(userId);
        await db.HashSetAsync(userKey, new HashEntry[]
        {
            new HashEntry("status", PresenceStatus.Offline.ToString()),
            new HashEntry("lastSeen", now)
        });

        // Remove connectionId field if present
        await db.HashDeleteAsync(userKey, "connectionId").ConfigureAwait(false);

        // Remove from all server sorted sets
        var tasks = serverIds.Select(serverId =>
        {
            var serverKey = GetServerPresenceKey(serverId);
            return db.SortedSetRemoveAsync(serverKey, userId);
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private string GetUserPresenceKey(long userId) => $"{_prefix}:presence:user:{userId}";
    private string GetServerPresenceKey(long serverId) => $"{_prefix}:presence:server:{serverId}";
}
