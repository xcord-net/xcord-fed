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
/// Service for resolving and checking user roles in servers and channels.
/// Implements Discord's role resolution algorithm.
/// For bots, roles are capped by the BotToken.Roles bitfield.
/// Role results are cached in Redis with a 60-second TTL to reduce database load.
/// </summary>
public sealed class RoleService : IRoleService
{
    private readonly AppDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly string _prefix;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RoleService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public RoleService(
        AppDbContext dbContext,
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RoleService> logger)
    {
        _dbContext = dbContext;
        _redis = redis;
        _prefix = redisOptions.Value.ChannelPrefix;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<long> GetServerRoles(long userId, long serverId)
    {
        var cacheKey = $"{_prefix}:perms:server:{userId}:{serverId}";

        try
        {
            var db = _redis.GetDatabase();
            var cached = await db.StringGetAsync(cacheKey).ConfigureAwait(false);
            if (cached.HasValue && long.TryParse(cached.ToString(), out var cachedPerms))
            {
                return cachedPerms;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable when reading server role cache for user {UserId} server {ServerId}; falling back to DB", userId, serverId);
        }

        var (roles, _) = await GetServerRolesWithEveryoneGroup(userId, serverId).ConfigureAwait(false);

        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(cacheKey, roles.ToString(), CacheTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable when writing server role cache for user {UserId} server {ServerId}", userId, serverId);
        }

        return roles;
    }

    /// <summary>
    /// Internal implementation that returns server roles along with the @everyone group.
    /// Used by GetChannelRoles to share the @everyone group lookup, avoiding a redundant
    /// DB round-trip that would otherwise duplicate the query in both methods.
    /// This method always queries the database - callers are responsible for cache lookup.
    /// </summary>
    private async Task<(long Roles, Group? EveryoneGroup)> GetServerRolesWithEveryoneGroup(long userId, long serverId)
    {
        // Step 1: Check if user is server owner
        var server = await _dbContext.Servers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serverId);

        if (server == null)
        {
            _logger.LogWarning("Server {ServerId} not found during role resolution", serverId);
            return (0L, null);
        }

        if (server.OwnerId == userId)
        {
            // Server owner has all roles - no need to load groups
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

        // Step 3: Batch-load all groups for the server in a single query, covering both
        // @everyone and member-assigned groups. GetChannelRoles reuses the @everyone
        // group returned here, avoiding a redundant re-fetch.
        var allServerGroups = await _dbContext.Groups
            .AsNoTracking()
            .Where(g => g.ServerId == serverId)
            .ToListAsync();

        var everyoneGroup = allServerGroups.FirstOrDefault(g => g.IsEveryone);

        // Step 4: Load the member's assigned group IDs
        var memberGroupIds = await _dbContext.MemberGroups
            .AsNoTracking()
            .Where(mg => mg.UserId == userId && mg.ServerId == serverId)
            .Select(mg => mg.GroupId)
            .ToListAsync();

        long roles = everyoneGroup?.Roles ?? 0L;

        // Step 5: OR all assigned group roles using the already-loaded group list (no extra DB query)
        roles |= allServerGroups
            .Where(g => memberGroupIds.Contains(g.Id))
            .Aggregate(0L, (acc, group) => acc | group.Roles);

        // Step 6: If user has Administrator, grant all roles
        if ((roles & (long)XcordRole.Administrator) != 0)
        {
            roles = long.MaxValue;
        }

        // Step 7: Cap roles for bots
        return (CapBotRoles(roles), everyoneGroup);
    }

    public async Task<long> GetChannelRoles(long userId, long channelId)
    {
        var cacheKey = $"{_prefix}:perms:channel:{userId}:{channelId}";

        try
        {
            var db = _redis.GetDatabase();
            var cached = await db.StringGetAsync(cacheKey).ConfigureAwait(false);
            if (cached.HasValue && long.TryParse(cached.ToString(), out var cachedPerms))
            {
                return cachedPerms;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable when reading channel role cache for user {UserId} channel {ChannelId}; falling back to DB", userId, channelId);
        }

        // Local helper: cache the result and return it
        async Task<long> CacheAndReturn(long result)
        {
            try
            {
                var cacheDb = _redis.GetDatabase();
                await cacheDb.StringSetAsync(cacheKey, result.ToString(), CacheTtl).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis unavailable when writing channel role cache for user {UserId} channel {ChannelId}", userId, channelId);
            }
            return result;
        }

        // Step 1: Get channel to find serverId
        var channel = await _dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == channelId);

        if (channel == null)
        {
            _logger.LogWarning("Channel {ChannelId} not found during role resolution", channelId);
            return 0L;
        }

        // Step 2: Get server roles - reuse the @everyone group from the internal implementation
        // to avoid a second DB round-trip that the original code made at step 3 below.
        var (roles, everyoneGroup) = await GetServerRolesWithEveryoneGroup(userId, channel.ServerId).ConfigureAwait(false);

        // If user has no server roles or is Administrator, cache and return early
        if (roles == 0L)
        {
            return await CacheAndReturn(0L).ConfigureAwait(false);
        }

        if (roles == long.MaxValue)
        {
            return await CacheAndReturn(long.MaxValue).ConfigureAwait(false); // Administrator has all roles
        }

        // Step 3: @everyone group already loaded by GetServerRolesWithEveryoneGroup - no extra query
        if (everyoneGroup == null)
        {
            _logger.LogWarning(
                "Server {ServerId} has no @everyone group during channel role resolution",
                channel.ServerId);
            return await CacheAndReturn(roles).ConfigureAwait(false); // Return server roles without overrides
        }

        // Step 4: Apply @everyone channel override
        var everyoneOverride = await _dbContext.ChannelPermissionOverrides
            .AsNoTracking()
            .FirstOrDefaultAsync(cpo =>
                cpo.ChannelId == channelId &&
                cpo.TargetType == OverrideTargetType.Group &&
                cpo.TargetId == everyoneGroup.Id);

        if (everyoneOverride != null)
        {
            roles = (roles & ~everyoneOverride.Deny) | everyoneOverride.Allow;
        }

        // Step 5: Get user's group IDs
        var userGroupIds = await _dbContext.MemberGroups
            .AsNoTracking()
            .Where(mg => mg.UserId == userId && mg.ServerId == channel.ServerId)
            .Select(mg => mg.GroupId)
            .ToListAsync();

        // Step 6: Apply group overrides (deny first, then allow)
        if (userGroupIds.Count > 0)
        {
            var roleOverrides = await _dbContext.ChannelPermissionOverrides
                .AsNoTracking()
                .Where(cpo =>
                    cpo.ChannelId == channelId &&
                    cpo.TargetType == OverrideTargetType.Group &&
                    userGroupIds.Contains(cpo.TargetId))
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
            roles = (roles & ~combinedDeny) | combinedAllow;
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
            roles = (roles & ~userOverride.Deny) | userOverride.Allow;
        }

        // Step 8: apply any group restriction on the channel itself, cap for bots, cache
        var gated = await ApplyAccessGroups(
            userId, new[] { channelId }, new Dictionary<long, long> { [channelId] = CapBotRoles(roles) })
            .ConfigureAwait(false);
        return await CacheAndReturn(gated[channelId]).ConfigureAwait(false);
    }

    /// <summary>
    /// Strip ViewChannels from channels reserved for a group the user is not in.
    /// </summary>
    /// <remarks>
    /// `Channel.AccessGroupId` is what "restrict this channel to a group" writes,
    /// and until now nothing read it: the column was stored, the UI reported
    /// success, and the channel stayed visible and reachable for everyone. It is
    /// applied after overrides so an explicit override cannot be used to walk
    /// around it, and never to an Administrator, who is already exempt from
    /// channel overrides everywhere else.
    /// </remarks>
    private async Task<Dictionary<long, long>> ApplyAccessGroups(
        long userId,
        IReadOnlyCollection<long> channelIds,
        Dictionary<long, long> roles)
    {
        if (channelIds.Count == 0) return roles;

        var restricted = await _dbContext.Channels
            .AsNoTracking()
            .Where(c => channelIds.Contains(c.Id) && c.AccessGroupId != null)
            .Select(c => new { c.Id, AccessGroupId = c.AccessGroupId!.Value, c.ServerId })
            .ToListAsync().ConfigureAwait(false);
        if (restricted.Count == 0) return roles;

        var groupIds = restricted.Select(r => r.AccessGroupId).Distinct().ToList();
        var memberOf = await _dbContext.MemberGroups
            .AsNoTracking()
            .Where(mg => mg.UserId == userId && groupIds.Contains(mg.GroupId))
            .Select(mg => mg.GroupId)
            .ToListAsync().ConfigureAwait(false);
        var memberOfSet = memberOf.ToHashSet();

        foreach (var channel in restricted)
        {
            if (!roles.TryGetValue(channel.Id, out var value)) continue;
            if (value == long.MaxValue) continue;                 // Administrator
            if ((value & (long)Xcord.Entities.Role.Administrator) != 0) continue;
            if (memberOfSet.Contains(channel.AccessGroupId)) continue;
            roles[channel.Id] = value & ~(long)Xcord.Entities.Role.ViewChannels;
        }

        return roles;
    }

    public async Task<Dictionary<long, long>> GetChannelRolesForServer(long userId, long serverId, IReadOnlyCollection<long> channelIds)
    {
        var results = new Dictionary<long, long>(channelIds.Count);
        var missing = new List<long>();

        // Bulk cache read: one MGET instead of one round-trip per channel.
        try
        {
            var db = _redis.GetDatabase();
            var keys = channelIds.Select(id => (RedisKey)$"{_prefix}:perms:channel:{userId}:{id}").ToArray();
            var cached = await db.StringGetAsync(keys).ConfigureAwait(false);
            var index = 0;
            foreach (var channelId in channelIds)
            {
                if (cached[index].HasValue && long.TryParse(cached[index].ToString(), out var cachedPerms))
                {
                    results[channelId] = cachedPerms;
                }
                else
                {
                    missing.Add(channelId);
                }
                index++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable when bulk-reading channel role cache for user {UserId}; falling back to DB", userId);
            results.Clear();
            missing = channelIds.ToList();
        }

        if (missing.Count == 0)
        {
            return results;
        }

        // Server-level roles and the @everyone group are channel-independent;
        // resolve them once for every cache miss.
        var (serverRoles, everyoneGroup) = await GetServerRolesWithEveryoneGroup(userId, serverId).ConfigureAwait(false);

        var computed = new Dictionary<long, long>(missing.Count);

        if (serverRoles == 0L || serverRoles == long.MaxValue || everyoneGroup == null)
        {
            // Mirrors the single-channel early returns: no roles, Administrator,
            // or a server missing its @everyone group all skip channel overrides.
            if (everyoneGroup == null && serverRoles != 0L && serverRoles != long.MaxValue)
            {
                _logger.LogWarning(
                    "Server {ServerId} has no @everyone group during channel role resolution",
                    serverId);
            }

            foreach (var channelId in missing)
            {
                computed[channelId] = serverRoles;
            }
        }
        else
        {
            var userGroupIds = await _dbContext.MemberGroups
                .AsNoTracking()
                .Where(mg => mg.UserId == userId && mg.ServerId == serverId)
                .Select(mg => mg.GroupId)
                .ToListAsync().ConfigureAwait(false);

            // One query for every override relevant to this user across all
            // missing channels, then apply them per channel in the same order
            // as GetChannelRoles: @everyone -> groups (deny then allow) -> user.
            var overrides = await _dbContext.ChannelPermissionOverrides
                .AsNoTracking()
                .Where(cpo => missing.Contains(cpo.ChannelId) &&
                    ((cpo.TargetType == OverrideTargetType.Group &&
                        (cpo.TargetId == everyoneGroup.Id || userGroupIds.Contains(cpo.TargetId))) ||
                     (cpo.TargetType == OverrideTargetType.User && cpo.TargetId == userId)))
                .ToListAsync().ConfigureAwait(false);

            var overridesByChannel = overrides
                .GroupBy(cpo => cpo.ChannelId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var channelId in missing)
            {
                var roles = serverRoles;

                if (overridesByChannel.TryGetValue(channelId, out var channelOverrides))
                {
                    var everyoneOverride = channelOverrides.FirstOrDefault(cpo =>
                        cpo.TargetType == OverrideTargetType.Group && cpo.TargetId == everyoneGroup.Id);
                    if (everyoneOverride != null)
                    {
                        roles = (roles & ~everyoneOverride.Deny) | everyoneOverride.Allow;
                    }

                    long combinedDeny = 0L;
                    long combinedAllow = 0L;
                    foreach (var groupOverride in channelOverrides.Where(cpo =>
                        cpo.TargetType == OverrideTargetType.Group && cpo.TargetId != everyoneGroup.Id))
                    {
                        combinedDeny |= groupOverride.Deny;
                        combinedAllow |= groupOverride.Allow;
                    }
                    roles = (roles & ~combinedDeny) | combinedAllow;

                    var userOverride = channelOverrides.FirstOrDefault(cpo =>
                        cpo.TargetType == OverrideTargetType.User && cpo.TargetId == userId);
                    if (userOverride != null)
                    {
                        roles = (roles & ~userOverride.Deny) | userOverride.Allow;
                    }
                }

                computed[channelId] = CapBotRoles(roles);
            }
        }

        computed = await ApplyAccessGroups(userId, missing, computed).ConfigureAwait(false);

        // Bulk cache write, pipelined in a single batch.
        try
        {
            var cacheDb = _redis.GetDatabase();
            var batch = cacheDb.CreateBatch();
            var writes = computed
                .Select(kv => batch.StringSetAsync(
                    $"{_prefix}:perms:channel:{userId}:{kv.Key}", kv.Value.ToString(), CacheTtl))
                .ToArray();
            batch.Execute();
            await Task.WhenAll(writes).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable when bulk-writing channel role cache for user {UserId}", userId);
        }

        foreach (var kv in computed)
        {
            results[kv.Key] = kv.Value;
        }

        return results;
    }

    public async Task<Result<bool>> EnsureServerRole(
        long userId,
        long serverId,
        XcordRole role)
    {
        var userRoles = await GetServerRoles(userId, serverId).ConfigureAwait(false);

        if ((userRoles & (long)role) != 0)
        {
            return true;
        }

        _logger.LogWarning(
            "User {UserId} denied server role {Role} in server {ServerId}",
            userId, role, serverId);

        return Error.Forbidden(
            "MISSING_PERMISSIONS",
            $"You do not have the required permission: {role}");
    }

    public async Task<Result<bool>> EnsureChannelRole(
        long userId,
        long channelId,
        XcordRole role)
    {
        var userRoles = await GetChannelRoles(userId, channelId).ConfigureAwait(false);

        if ((userRoles & (long)role) != 0)
        {
            return true;
        }

        _logger.LogWarning(
            "User {UserId} denied channel role {Role} in channel {ChannelId}",
            userId, role, channelId);

        return Error.Forbidden(
            "MISSING_PERMISSIONS",
            $"You do not have the required permission: {role}");
    }

    /// <inheritdoc />
    public async Task<int> GetHighestGroupPosition(long userId, long serverId)
    {
        // Server owner outranks everyone
        var server = await _dbContext.Servers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serverId);

        if (server == null) return 0;
        if (server.OwnerId == userId) return int.MaxValue;

        // Get the user's assigned group IDs
        var memberGroupIds = await _dbContext.MemberGroups
            .AsNoTracking()
            .Where(mg => mg.UserId == userId && mg.ServerId == serverId)
            .Select(mg => mg.GroupId)
            .ToListAsync();

        if (memberGroupIds.Count == 0) return 0;

        // Get the highest position among the user's groups
        var highestPosition = await _dbContext.Groups
            .AsNoTracking()
            .Where(g => memberGroupIds.Contains(g.Id))
            .MaxAsync(g => (int?)g.Position) ?? 0;

        return highestPosition;
    }

    /// <inheritdoc />
    public async Task InvalidateUserRolesAsync(long userId, long serverId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();

            // Delete server-level role cache entry for this user
            var serverKey = $"{_prefix}:perms:server:{userId}:{serverId}";
            await db.KeyDeleteAsync(serverKey).ConfigureAwait(false);

            // Delete channel-level role cache entries for every channel in the server.
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

                await db.KeyDeleteAsync(channelKeys).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Redis unavailable when invalidating role cache for user {UserId} in server {ServerId}",
                userId, serverId);
        }
    }

    /// <inheritdoc />
    public async Task InvalidateGroupMembersRolesAsync(long groupId, long serverId, CancellationToken cancellationToken = default)
    {
        // Check if this is the @everyone group. The @everyone group applies to ALL server
        // members implicitly - its membership is NOT tracked in MemberGroups. If we only
        // query MemberGroups we'd find zero affected users and skip cache invalidation.
        var isEveryoneGroup = await _dbContext.Groups
            .AsNoTracking()
            .AnyAsync(g => g.Id == groupId && g.IsEveryone, cancellationToken);

        List<long> affectedUserIds;
        if (isEveryoneGroup)
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
            // Find every user who currently holds this group in the server.
            // We must do this before the group assignment rows are deleted so that
            // DeleteGroupHandler can call this before SaveChanges, but AssignGroupHandler/
            // RemoveGroupHandler call it after - the group membership rows still exist when
            // UpdateGroupHandler triggers this. For DeleteGroup we query before soft-delete.
            affectedUserIds = await _dbContext.MemberGroups
                .AsNoTracking()
                .Where(mg => mg.GroupId == groupId && mg.ServerId == serverId)
                .Select(mg => mg.UserId)
                .ToListAsync(cancellationToken);
        }

        foreach (var uid in affectedUserIds)
        {
            await InvalidateUserRolesAsync(uid, serverId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Caps roles for bot users based on the bot_roles claim from BotToken.
    /// If the current user is a bot, the returned roles are ANDed with the token's role cap.
    /// </summary>
    private long CapBotRoles(long roles)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return roles;
        }

        // Check if the current user is a bot
        var isBotClaim = httpContext.User.FindFirst("bot")?.Value;
        if (isBotClaim != "true")
        {
            return roles;
        }

        // Get the bot_roles claim (role cap from BotToken)
        var botRolesClaim = httpContext.User.FindFirst("bot_roles")?.Value;
        if (string.IsNullOrEmpty(botRolesClaim) || !long.TryParse(botRolesClaim, out var botRoleCap))
        {
            _logger.LogWarning("Bot user has no valid bot_roles claim, denying all roles");
            return 0L;
        }

        // Cap the roles: only allow roles that are both granted AND in the token's role cap
        return roles & botRoleCap;
    }
}
