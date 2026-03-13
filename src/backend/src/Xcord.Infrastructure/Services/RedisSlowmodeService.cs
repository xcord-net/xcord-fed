using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Redis-backed implementation of slowmode enforcement.
/// Stores the last-message Unix timestamp per user per channel as a simple Redis string.
/// The key's TTL is set to the slowmode interval so it auto-expires when the cooldown ends.
/// </summary>
public sealed class RedisSlowmodeService : ISlowmodeService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _prefix;

    public RedisSlowmodeService(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions)
    {
        _redis = redis;
        _prefix = redisOptions.Value.ChannelPrefix;
    }

    /// <inheritdoc />
    public async Task<int> CheckAndRecordAsync(long userId, long channelId, int slowModeSeconds)
    {
        var db = _redis.GetDatabase();
        var key = $"{_prefix}:slowmode:{channelId}:{userId}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Attempt to set the key only if it does not already exist (NX = Not eXists).
        // TTL = slowModeSeconds so the entry auto-expires once the cooldown ends.
        var set = await db.StringSetAsync(
            key,
            now,
            TimeSpan.FromSeconds(slowModeSeconds),
            When.NotExists);

        if (set)
        {
            // Key did not exist - the user is allowed to send; cooldown window now starts.
            return 0;
        }

        // Key already existed - the user is still in cooldown.
        // Calculate remaining seconds from the key TTL.
        var ttl = await db.KeyTimeToLiveAsync(key);
        var remaining = ttl.HasValue ? (int)Math.Ceiling(ttl.Value.TotalSeconds) : slowModeSeconds;
        return Math.Max(1, remaining);
    }
}
