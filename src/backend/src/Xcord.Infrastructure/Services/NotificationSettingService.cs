using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Redis-cached implementation of notification setting resolution service.
/// Resolution order: Channel > Server > Global (most specific wins).
/// </summary>
public sealed class NotificationSettingService : INotificationSettingService
{
    private readonly AppDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly string _prefix;
    private readonly ILogger<NotificationSettingService> _logger;

    public NotificationSettingService(
        AppDbContext dbContext,
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions,
        ILogger<NotificationSettingService> logger)
    {
        _dbContext = dbContext;
        _redis = redis;
        _prefix = redisOptions.Value.ChannelPrefix;
        _logger = logger;
    }

    public async Task<NotificationLevel> ResolveAsync(long userId, long? serverId, long? channelId)
    {
        // Try to get from cache first
        var cacheKey = GetCacheKey(userId, serverId, channelId);
        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync(cacheKey).ConfigureAwait(false);

        if (!cached.IsNullOrEmpty && Enum.TryParse<NotificationLevel>(cached.ToString(), out var cachedLevel))
        {
            return cachedLevel;
        }

        // Query database with resolution order: Channel > Server > Global
        var settings = await _dbContext.NotificationSettings
            .Where(ns => ns.UserId == userId &&
                        (ns.ChannelId == channelId && ns.ServerId == serverId ||
                         ns.ChannelId == null && ns.ServerId == serverId ||
                         ns.ChannelId == null && ns.ServerId == null))
            .OrderByDescending(ns => ns.ChannelId != null ? 3 : (ns.ServerId != null ? 2 : 1))
            .ToListAsync();

        NotificationLevel level = NotificationLevel.All; // Default
        DateTimeOffset? muteUntil = null;

        // Find most specific setting
        foreach (var setting in settings)
        {
            // Channel-specific setting
            if (setting.ChannelId == channelId && channelId.HasValue)
            {
                level = setting.Level;
                muteUntil = setting.MuteUntil;
                break;
            }
            // Server-specific setting
            if (setting.ServerId == serverId && serverId.HasValue && setting.ChannelId == null)
            {
                level = setting.Level;
                muteUntil = setting.MuteUntil;
                break;
            }
            // Global setting
            if (setting.ServerId == null && setting.ChannelId == null)
            {
                level = setting.Level;
                muteUntil = setting.MuteUntil;
                break;
            }
        }

        // If muted, treat as None
        if (muteUntil.HasValue && muteUntil.Value > DateTimeOffset.UtcNow)
        {
            level = NotificationLevel.None;
        }

        // Cache for 5 minutes
        await db.StringSetAsync(cacheKey, level.ToString(), TimeSpan.FromMinutes(5)).ConfigureAwait(false);

        return level;
    }

    public async Task<bool> ShouldNotifyAsync(long userId, long? serverId, long? channelId, bool isEveryone, bool isRoleMention)
    {
        // Get the effective notification level
        var level = await ResolveAsync(userId, serverId, channelId).ConfigureAwait(false);

        // If level is None, never notify
        if (level == NotificationLevel.None)
        {
            return false;
        }

        // Get the most specific setting to check suppression flags
        var setting = await GetMostSpecificSettingAsync(userId, serverId, channelId).ConfigureAwait(false);

        // Check suppression flags
        if (setting != null)
        {
            if (isEveryone && setting.SuppressEveryone)
            {
                return false;
            }

            if (isRoleMention && setting.SuppressRoles)
            {
                return false;
            }
        }

        // If level is MentionsOnly, only notify for mentions
        if (level == NotificationLevel.MentionsOnly)
        {
            // This assumes the caller only calls ShouldNotifyAsync when a mention is present
            // For non-mention messages, they should check the level first
            return true;
        }

        // Level is All, notify
        return true;
    }

    private async Task<Entities.NotificationSetting?> GetMostSpecificSettingAsync(long userId, long? serverId, long? channelId)
    {
        var settings = await _dbContext.NotificationSettings
            .Where(ns => ns.UserId == userId &&
                        (ns.ChannelId == channelId && ns.ServerId == serverId ||
                         ns.ChannelId == null && ns.ServerId == serverId ||
                         ns.ChannelId == null && ns.ServerId == null))
            .OrderByDescending(ns => ns.ChannelId != null ? 3 : (ns.ServerId != null ? 2 : 1))
            .ToListAsync();

        // Return most specific setting
        foreach (var setting in settings)
        {
            if (setting.ChannelId == channelId && channelId.HasValue)
            {
                return setting;
            }
            if (setting.ServerId == serverId && serverId.HasValue && setting.ChannelId == null)
            {
                return setting;
            }
            if (setting.ServerId == null && setting.ChannelId == null)
            {
                return setting;
            }
        }

        return null;
    }

    private string GetCacheKey(long userId, long? serverId, long? channelId)
    {
        return $"{_prefix}:notif:{userId}:{serverId ?? 0}:{channelId ?? 0}";
    }
}
