using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Claims;
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
        var userIdentifier = invocationContext.Context.UserIdentifier
            ?? invocationContext.Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var rateKeyPart = !string.IsNullOrEmpty(userIdentifier)
            ? $"user:{userIdentifier}"
            : $"conn:{connectionId}";
        var key = $"{_channelPrefix}:hubrate:{rateKeyPart}";
        var db = _redis.GetDatabase();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - WindowSeconds;

        await db.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);

        var count = await db.SortedSetLengthAsync(key);

        if (count >= MaxInvocations)
        {
            _logger.LogWarning(
                "Rate limit exceeded for {RateKey} on method {Method} (connection {ConnectionId})",
                rateKeyPart,
                invocationContext.HubMethodName,
                connectionId);

            throw new HubException("Rate limit exceeded. Please slow down.");
        }

        await db.SortedSetAddAsync(key, Guid.NewGuid().ToString(), now);

        await db.KeyExpireAsync(key, TimeSpan.FromSeconds(WindowSeconds * 2));

        return await next(invocationContext);
    }
}
