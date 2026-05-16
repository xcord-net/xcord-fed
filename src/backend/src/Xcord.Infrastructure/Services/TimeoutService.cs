using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Implementation of ITimeoutService using Redis cache + database fallback.
/// </summary>
public sealed class TimeoutService : ITimeoutService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly AppDbContext _dbContext;
    private readonly string _redisPrefix;

    public TimeoutService(
        IConnectionMultiplexer redis,
        AppDbContext dbContext,
        IOptions<RedisOptions> redisOptions)
    {
        _redis = redis;
        _dbContext = dbContext;
        _redisPrefix = redisOptions.Value.ChannelPrefix;
    }

    public async Task<bool> IsTimedOutAsync(long userId, long serverId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{_redisPrefix}:timeout:{serverId}:{userId}";

        // Check Redis first
        var cachedExpiry = await db.StringGetAsync(key).ConfigureAwait(false);
        if (cachedExpiry.HasValue)
        {
            if (DateTimeOffset.TryParse(cachedExpiry!, out var expiresAt))
            {
                return DateTimeOffset.UtcNow < expiresAt;
            }
        }

        // Fallback to database
        var timeout = await _dbContext.Timeouts
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.ServerId == serverId && t.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(t => t.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (timeout != null)
        {
            // Cache it in Redis with TTL
            var ttl = timeout.ExpiresAt - DateTimeOffset.UtcNow;
            if (ttl.TotalSeconds > 0)
            {
                await db.StringSetAsync(key, timeout.ExpiresAt.ToString("O"), ttl).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    public async Task SetTimeoutAsync(long userId, long serverId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{_redisPrefix}:timeout:{serverId}:{userId}";

        var ttl = expiresAt - DateTimeOffset.UtcNow;
        if (ttl.TotalSeconds > 0)
        {
            await db.StringSetAsync(key, expiresAt.ToString("O"), ttl).ConfigureAwait(false);
        }
    }

    public async Task RemoveTimeoutAsync(long userId, long serverId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{_redisPrefix}:timeout:{serverId}:{userId}";

        await db.KeyDeleteAsync(key).ConfigureAwait(false);
    }
}
