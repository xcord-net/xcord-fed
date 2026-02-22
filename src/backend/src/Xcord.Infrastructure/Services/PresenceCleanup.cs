using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that cleans up stale presence data.
/// Runs every 60 seconds and marks users as Offline if they haven't sent a heartbeat in 90 seconds.
/// </summary>
public sealed class PresenceCleanup : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly string _prefix;
    private readonly ILogger<PresenceCleanup> _logger;
    private const int IntervalSeconds = 60;
    private const int StaleTimeoutSeconds = 90;

    public PresenceCleanup(
        IServiceScopeFactory serviceScopeFactory,
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions,
        ILogger<PresenceCleanup> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _redis = redis;
        _prefix = redisOptions.Value.ChannelPrefix;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PresenceCleanup background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
                await CleanupStalePresenceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during presence cleanup");
            }
        }

        _logger.LogInformation("PresenceCleanup background service stopped");
    }

    private async Task CleanupStalePresenceAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();
        var presenceNotifier = scope.ServiceProvider.GetRequiredService<IPresenceNotifier>();
        var db = _redis.GetDatabase();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var staleThreshold = now - StaleTimeoutSeconds;

        // Find all server presence keys using SCAN
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var pattern = $"{_prefix}:presence:server:*";
        var keys = server.Keys(pattern: pattern);

        foreach (var key in keys)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            // Extract serverId from key
            var keyString = key.ToString();
            var parts = keyString.Split(':');
            if (parts.Length < 4 || !long.TryParse(parts[3], out var serverId))
                continue;

            // Find stale members (score < staleThreshold)
            var staleMembers = await db.SortedSetRangeByScoreAsync(
                key,
                double.NegativeInfinity,
                staleThreshold);

            if (staleMembers.Length == 0)
                continue;

            _logger.LogDebug("Found {Count} stale presence entries for server {ServerId}",
                staleMembers.Length, serverId);

            foreach (var member in staleMembers)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                if (!long.TryParse(member.ToString(), out var userId))
                    continue;

                // Check if user is a bot (skip bots)
                var user = await context.Users
                    .Where(u => u.Id == userId && u.DeletedAt == null)
                    .FirstOrDefaultAsync(stoppingToken);

                if (user == null || user.IsBot)
                    continue;

                // Set user to Offline
                await presenceService.SetStatusAsync(userId, PresenceStatus.Offline);

                // Remove from server sorted set
                await db.SortedSetRemoveAsync(key, userId);

                // Notify the server group
                await presenceNotifier.NotifyPresenceChangedAsync(
                    userId,
                    PresenceStatus.Offline,
                    new[] { serverId });

                _logger.LogDebug("Marked user {UserId} as Offline in server {ServerId}", userId, serverId);
            }
        }
    }
}
