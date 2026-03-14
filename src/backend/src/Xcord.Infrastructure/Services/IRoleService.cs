using Xcord.Entities;
using Xcord;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for resolving and checking user roles in servers and channels.
/// </summary>
public interface IRoleService
{
    /// <summary>
    /// Gets the resolved server-level roles for a user.
    /// Resolution order: owner check -> @everyone group -> OR all assigned groups -> Administrator check.
    /// </summary>
    Task<long> GetServerRoles(long userId, long serverId);

    /// <summary>
    /// Gets the resolved channel-level roles for a user.
    /// Resolution order: server roles -> @everyone override -> group overrides -> user override.
    /// </summary>
    Task<long> GetChannelRoles(long userId, long channelId);

    /// <summary>
    /// Ensures the user has the specified server role.
    /// Returns success if authorized, or a Forbidden error if not.
    /// </summary>
    Task<Result<bool>> EnsureServerRole(long userId, long serverId, Role role);

    /// <summary>
    /// Ensures the user has the specified channel role.
    /// Returns success if authorized, or a Forbidden error if not.
    /// </summary>
    Task<Result<bool>> EnsureChannelRole(long userId, long channelId, Role role);

    /// <summary>
    /// Gets the highest group position held by a user in a server.
    /// Returns 0 if the user has no assigned groups (only @everyone).
    /// Returns int.MaxValue for the server owner.
    /// </summary>
    Task<int> GetHighestGroupPosition(long userId, long serverId);

    /// <summary>
    /// Invalidates all cached role entries (server-level and channel-level) for a single user
    /// in a given server. Called after a group is assigned to or removed from a specific user.
    /// </summary>
    Task InvalidateUserRolesAsync(long userId, long serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cached role entries (server-level and channel-level) for every member
    /// who holds a given group in a server. Called after a group's roles are changed or the
    /// group is deleted.
    /// </summary>
    Task InvalidateGroupMembersRolesAsync(long groupId, long serverId, CancellationToken cancellationToken = default);
}
