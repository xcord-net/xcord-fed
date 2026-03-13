using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord;
using XcordRole = Xcord.Entities.Role;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for resolving and checking user permissions in servers and channels.
/// Implements Discord's permission resolution algorithm.
/// For bots, permissions are capped by the BotToken.Permissions bitfield.
/// Permission results are cached in Redis with a 60-second TTL to reduce database load.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly AppDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly string _prefix;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PermissionService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public PermissionService(
        AppDbContext dbContext,
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PermissionService> logger)
    {
        _dbContext = dbContext;
        _redis = redis;
        _prefix = redisOptions.Value.ChannelPrefix;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<long> GetServerPermissions(long userId, long serverId)
    {
        var cacheKey = $"{_prefix}:perms:server:{userId}:{serverId}";

        try
        {
            var db = _redis.GetDatabase();
            var cached = await db.StringGetAsync(cacheKey);
            if (cached.HasValue && long.TryParse(cached.ToString(), out var cachedPerms))
            {
                return cachedPerms;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable when reading server permission cache for user {UserId} server {ServerId}; falling back to DB", userId, serverId);
        }

        var (permissions, _) = await GetServerPermissionsWithEveryoneRole(userId, serverId);

        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(cacheKey, permissions.ToString(), CacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable when writing server permission cache for user {UserId} server {ServerId}", userId, serverId);
        }

        return permissions;
    }

    /// <summary>
    /// Internal implementation that returns server permissions along with the @everyone role.
    /// Used by GetChannelPermissions to share the @everyone role lookup, avoiding a redundant
    /// DB round-trip that would otherwise duplicate the query in both methods.
    /// This method always queries the database - callers are responsible for cache lookup.
    /// </summary>
    private async Task<(long Permissions, XcordRole? EveryoneRole)> GetServerPermissionsWithEveryoneRole(long userId, long serverId)
    {
        // Step 1: Check if user is server owner
        var server = await _dbContext.Servers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serverId);

        if (server == null)
        {
            _logger.LogWarning("Server {ServerId} not found during permission resolution", serverId);
            return (0L, null);
        }

        if (server.OwnerId == userId)
        {
            // Server owner has all permissions - no need to load roles
            return (long.MaxValue, null);
        }

        // Step 2: Check if user is a member
        var isMember = await _dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == serverId);

        if (!isMember)
        {
            _logger.LogWarning(
                "User {UserId} is not a member of server {ServerId}",
                userId, serverId);
            return (0L, null);
        }

        // Step 3: Batch-load all roles for the server in a single query, covering both
        // @everyone and member-assigned roles. GetChannelPermissions reuses the @everyone
        // role returned here, avoiding a redundant re-fetch.
        var allServerRoles = await _dbContext.Roles
            .AsNoTracking()
            .Where(r => r.ServerId == serverId)
            .ToListAsync();

        var everyoneRole = allServerRoles.FirstOrDefault(r => r.IsEveryone);

        // Step 4: Load the member's assigned role IDs
        var memberRoleIds = await _dbContext.MemberRoles
            .AsNoTracking()
            .Where(mr => mr.UserId == userId && mr.ServerId == serverId)
            .Select(mr => mr.RoleId)
            .ToListAsync();

        long permissions = everyoneRole?.Permissions ?? 0L;

        // Step 5: OR all assigned role permissions using the already-loaded role list (no extra DB query)
        permissions |= allServerRoles
            .Where(r => memberRoleIds.Contains(r.Id))
            .Aggregate(0L, (acc, role) => acc | role.Permissions);

        // Step 6: If user has Administrator, grant all permissions
        if ((permissions & (long)Permission.Administrator) != 0)
        {
            permissions = long.MaxValue;
        }

        // Step 7: Cap permissions for bots
        return (CapBotPermissions(permissions), everyoneRole);
    }

    public async Task<long> GetChannelPermissions(long userId, long channelId)
    {
        var cacheKey = $"{_prefix}:perms:channel:{userId}:{channelId}";

        try
        {
            var db = _redis.GetDatabase();
            var cached = await db.StringGetAsync(cacheKey);
            if (cached.HasValue && long.TryParse(cached.ToString(), out var cachedPerms))
            {
                return cachedPerms;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable when reading channel permission cache for user {UserId} channel {ChannelId}; falling back to DB", userId, channelId);
        }

        // Local helper: cache the result and return it
        async Task<long> CacheAndReturn(long result)
        {
            try
            {
                var cacheDb = _redis.GetDatabase();
                await cacheDb.StringSetAsync(cacheKey, result.ToString(), CacheTtl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis unavailable when writing channel permission cache for user {UserId} channel {ChannelId}", userId, channelId);
            }
            return result;
        }

        // Step 1: Get channel to find serverId
        var channel = await _dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == channelId);

        if (channel == null)
        {
            _logger.LogWarning("Channel {ChannelId} not found during permission resolution", channelId);
            return 0L;
        }

        // Step 2: Get server permissions - reuse the @everyone role from the internal implementation
        // to avoid a second DB round-trip that the original code made at step 3 below.
        var (permissions, everyoneRole) = await GetServerPermissionsWithEveryoneRole(userId, channel.ServerId);

        // If user has no server permissions or is Administrator, cache and return early
        if (permissions == 0L)
        {
            return await CacheAndReturn(0L);
        }

        if (permissions == long.MaxValue)
        {
            return await CacheAndReturn(long.MaxValue); // Administrator has all permissions
        }

        // Step 3: @everyone role already loaded by GetServerPermissionsWithEveryoneRole - no extra query
        if (everyoneRole == null)
        {
            _logger.LogWarning(
                "Server {ServerId} has no @everyone role during channel permission resolution",
                channel.ServerId);
            return await CacheAndReturn(permissions); // Return server permissions without overrides
        }

        // Step 4: Apply @everyone channel override
        var everyoneOverride = await _dbContext.ChannelPermissionOverrides
            .AsNoTracking()
            .FirstOrDefaultAsync(cpo =>
                cpo.ChannelId == channelId &&
                cpo.TargetType == OverrideTargetType.Role &&
                cpo.TargetId == everyoneRole.Id);

        if (everyoneOverride != null)
        {
            permissions = (permissions & ~everyoneOverride.Deny) | everyoneOverride.Allow;
        }

        // Step 5: Get user's role IDs
        var userRoleIds = await _dbContext.MemberRoles
            .AsNoTracking()
            .Where(mr => mr.UserId == userId && mr.ServerId == channel.ServerId)
            .Select(mr => mr.RoleId)
            .ToListAsync();

        // Step 6: Apply role overrides (deny first, then allow)
        if (userRoleIds.Count > 0)
        {
            var roleOverrides = await _dbContext.ChannelPermissionOverrides
                .AsNoTracking()
                .Where(cpo =>
                    cpo.ChannelId == channelId &&
                    cpo.TargetType == OverrideTargetType.Role &&
                    userRoleIds.Contains(cpo.TargetId))
                .ToListAsync();

            // Combine all deny bitfields
            long combinedDeny = 0L;
            foreach (var roleOverride in roleOverrides)
            {
                combinedDeny |= roleOverride.Deny;
            }

            // Combine all allow bitfields
            long combinedAllow = 0L;
            foreach (var roleOverride in roleOverrides)
            {
                combinedAllow |= roleOverride.Allow;
            }

            // Apply: deny first, then allow (allow wins over deny at same precedence level)
            permissions = (permissions & ~combinedDeny) | combinedAllow;
        }

        // Step 7: Apply user-specific override (highest priority)
        var userOverride = await _dbContext.ChannelPermissionOverrides
            .AsNoTracking()
            .FirstOrDefaultAsync(cpo =>
                cpo.ChannelId == channelId &&
                cpo.TargetType == OverrideTargetType.User &&
                cpo.TargetId == userId);

        if (userOverride != null)
        {
            permissions = (permissions & ~userOverride.Deny) | userOverride.Allow;
        }

        // Step 8: Cap permissions for bots and cache
        return await CacheAndReturn(CapBotPermissions(permissions));
    }

    public async Task<Result<bool>> EnsureServerPermission(
        long userId,
        long serverId,
        Permission permission)
    {
        var userPermissions = await GetServerPermissions(userId, serverId);

        if ((userPermissions & (long)permission) != 0)
        {
            return true;
        }

        _logger.LogWarning(
            "User {UserId} denied server permission {Permission} in server {ServerId}",
            userId, permission, serverId);

        return Error.Forbidden(
            "MISSING_PERMISSIONS",
            $"You do not have the required permission: {permission}");
    }

    public async Task<Result<bool>> EnsureChannelPermission(
        long userId,
        long channelId,
        Permission permission)
    {
        var userPermissions = await GetChannelPermissions(userId, channelId);

        if ((userPermissions & (long)permission) != 0)
        {
            return true;
        }

        _logger.LogWarning(
            "User {UserId} denied channel permission {Permission} in channel {ChannelId}",
            userId, permission, channelId);

        return Error.Forbidden(
            "MISSING_PERMISSIONS",
            $"You do not have the required permission: {permission}");
    }

    /// <inheritdoc />
    public async Task<int> GetHighestRolePosition(long userId, long serverId)
    {
        // Server owner outranks everyone
        var server = await _dbContext.Servers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serverId);

        if (server == null) return 0;
        if (server.OwnerId == userId) return int.MaxValue;

        // Get the user's assigned role IDs
        var memberRoleIds = await _dbContext.MemberRoles
            .AsNoTracking()
            .Where(mr => mr.UserId == userId && mr.ServerId == serverId)
            .Select(mr => mr.RoleId)
            .ToListAsync();

        if (memberRoleIds.Count == 0) return 0;

        // Get the highest position among the user's roles
        var highestPosition = await _dbContext.Roles
            .AsNoTracking()
            .Where(r => memberRoleIds.Contains(r.Id))
            .MaxAsync(r => (int?)r.Position) ?? 0;

        return highestPosition;
    }

    /// <inheritdoc />
    public async Task InvalidateUserPermissionsAsync(long userId, long serverId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();

            // Delete server-level permission cache entry for this user
            var serverKey = $"{_prefix}:perms:server:{userId}:{serverId}";
            await db.KeyDeleteAsync(serverKey);

            // Delete channel-level permission cache entries for every channel in the server.
            // We query the channel IDs so we can construct the exact keys (no SCAN needed).
            var channelIds = await _dbContext.Channels
                .AsNoTracking()
                .Where(c => c.ServerId == serverId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            if (channelIds.Count > 0)
            {
                var channelKeys = channelIds
                    .Select(cid => (RedisKey)$"{_prefix}:perms:channel:{userId}:{cid}")
                    .ToArray();

                await db.KeyDeleteAsync(channelKeys);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Redis unavailable when invalidating permission cache for user {UserId} in server {ServerId}",
                userId, serverId);
        }
    }

    /// <inheritdoc />
    public async Task InvalidateRoleMembersPermissionsAsync(long roleId, long serverId, CancellationToken cancellationToken = default)
    {
        // Check if this is the @everyone role. The @everyone role applies to ALL server
        // members implicitly - its membership is NOT tracked in MemberRoles. If we only
        // query MemberRoles we'd find zero affected users and skip cache invalidation.
        var isEveryoneRole = await _dbContext.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == roleId && r.IsEveryone, cancellationToken);

        List<long> affectedUserIds;
        if (isEveryoneRole)
        {
            // @everyone applies to ALL server members - invalidate everyone
            affectedUserIds = await _dbContext.ServerMembers
                .AsNoTracking()
                .Where(sm => sm.ServerId == serverId)
                .Select(sm => sm.UserId)
                .ToListAsync(cancellationToken);
        }
        else
        {
            // Find every user who currently holds this role in the server.
            // We must do this before the role assignment rows are deleted so that
            // DeleteRoleHandler can call this before SaveChanges, but AssignRoleHandler/
            // RemoveRoleHandler call it after - the role membership rows still exist when
            // UpdateRoleHandler triggers this. For DeleteRole we query before soft-delete.
            affectedUserIds = await _dbContext.MemberRoles
                .AsNoTracking()
                .Where(mr => mr.RoleId == roleId && mr.ServerId == serverId)
                .Select(mr => mr.UserId)
                .ToListAsync(cancellationToken);
        }

        foreach (var uid in affectedUserIds)
        {
            await InvalidateUserPermissionsAsync(uid, serverId, cancellationToken);
        }
    }

    /// <summary>
    /// Caps permissions for bot users based on the bot_permissions claim from BotToken.
    /// If the current user is a bot, the returned permissions are ANDed with the token's permission cap.
    /// </summary>
    private long CapBotPermissions(long permissions)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return permissions;
        }

        // Check if the current user is a bot
        var isBotClaim = httpContext.User.FindFirst("bot")?.Value;
        if (isBotClaim != "true")
        {
            return permissions;
        }

        // Get the bot_permissions claim (permission cap from BotToken)
        var botPermissionsClaim = httpContext.User.FindFirst("bot_permissions")?.Value;
        if (string.IsNullOrEmpty(botPermissionsClaim) || !long.TryParse(botPermissionsClaim, out var botPermissionCap))
        {
            _logger.LogWarning("Bot user has no valid bot_permissions claim, denying all permissions");
            return 0L;
        }

        // Cap the permissions: only allow permissions that are both granted AND in the token's permission cap
        return permissions & botPermissionCap;
    }
}
