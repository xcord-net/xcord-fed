using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Infrastructure.Options;

namespace Xcord.Api;

/// <summary>
/// Hub filter that implements rate limiting for SignalR hub method invocations.
/// Sliding window: 60 invocations per 10 seconds per connection.
/// </summary>
public class HubRateLimitFilter : IHubFilter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _channelPrefix;
    private readonly ILogger<HubRateLimitFilter> _logger;

    private const int MaxInvocations = 60;
    private const int WindowSeconds = 10;

    public HubRateLimitFilter(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions,
        ILogger<HubRateLimitFilter> logger)
    {
        _redis = redis;
        _channelPrefix = redisOptions.Value.ChannelPrefix;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var connectionId = invocationContext.Context.ConnectionId;
        var key = $"{_channelPrefix}:hubrate:{connectionId}";
        var db = _redis.GetDatabase();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - WindowSeconds;

        // Remove old entries outside the window
        await db.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);

        // Get current count in window
        var count = await db.SortedSetLengthAsync(key);

        if (count >= MaxInvocations)
        {
            _logger.LogWarning(
                "Rate limit exceeded for connection {ConnectionId} on method {Method}",
                connectionId,
                invocationContext.HubMethodName);

            // Send error to caller, do NOT disconnect
            throw new HubException("Rate limit exceeded. Please slow down.");
        }

        // Add current invocation to sorted set
        await db.SortedSetAddAsync(key, Guid.NewGuid().ToString(), now);

        // Set expiry on the key to clean up after window expires
        await db.KeyExpireAsync(key, TimeSpan.FromSeconds(WindowSeconds * 2));

        // Continue with the invocation
        return await next(invocationContext);
    }
}
