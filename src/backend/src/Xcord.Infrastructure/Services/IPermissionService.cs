using Xcord.Entities;
using Xcord;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for resolving and checking user permissions in servers and channels.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Gets the resolved server-level permissions for a user.
    /// Resolution order: owner check → @everyone role → OR all assigned roles → Administrator check.
    /// </summary>
    Task<long> GetServerPermissions(long userId, long serverId);

    /// <summary>
    /// Gets the resolved channel-level permissions for a user.
    /// Resolution order: server perms → @everyone override → role overrides → user override.
    /// </summary>
    Task<long> GetChannelPermissions(long userId, long channelId);

    /// <summary>
    /// Ensures the user has the specified server permission.
    /// Returns success if authorized, or a Forbidden error if not.
    /// </summary>
    Task<Result<bool>> EnsureServerPermission(long userId, long serverId, Permission permission);

    /// <summary>
    /// Ensures the user has the specified channel permission.
    /// Returns success if authorized, or a Forbidden error if not.
    /// </summary>
    Task<Result<bool>> EnsureChannelPermission(long userId, long channelId, Permission permission);

    /// <summary>
    /// Gets the highest role position held by a user in a server.
    /// Returns 0 if the user has no assigned roles (only @everyone).
    /// Returns int.MaxValue for the server owner.
    /// </summary>
    Task<int> GetHighestRolePosition(long userId, long serverId);

    /// <summary>
    /// Invalidates all cached permission entries (server-level and channel-level) for a single user
    /// in a given server. Called after a role is assigned to or removed from a specific user.
    /// </summary>
    Task InvalidateUserPermissionsAsync(long userId, long serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cached permission entries (server-level and channel-level) for every member
    /// who holds a given role in a server. Called after a role's permissions are changed or the
    /// role is deleted.
    /// </summary>
    Task InvalidateRoleMembersPermissionsAsync(long roleId, long serverId, CancellationToken cancellationToken = default);
}
