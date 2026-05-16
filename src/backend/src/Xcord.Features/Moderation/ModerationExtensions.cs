using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Moderation;

/// <summary>
/// Shared validation helpers for moderation actions (ban, kick, timeout).
/// </summary>
public static class ModerationExtensions
{
    /// <summary>
    /// Validates that a moderation action (ban/kick/timeout) is permissible.
    /// Checks self-moderation, server existence, owner protection, and role hierarchy.
    /// Returns the tracked Server entity on success so callers can mutate it (e.g. MemberCount--).
    /// </summary>
    public static async Task<Result<Server>> ValidateModerationTarget(
        this AppDbContext db,
        IRoleService roleService,
        long moderatorId,
        long targetUserId,
        long serverId,
        string action,
        CancellationToken ct)
    {
        // 1. Cannot moderate yourself
        if (moderatorId == targetUserId)
            return Error.Validation($"CANNOT_{action.ToUpperInvariant()}_SELF", $"You cannot {action} yourself");

        // 2. Load server and check owner protection
        var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == serverId, ct).ConfigureAwait(false);
        if (server == null)
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        if (server.OwnerId == targetUserId)
            return Error.Validation($"CANNOT_{action.ToUpperInvariant()}_OWNER", $"You cannot {action} the server owner");

        // 3. Role hierarchy check
        var moderatorHighest = await roleService.GetHighestGroupPosition(moderatorId, serverId).ConfigureAwait(false);
        var targetHighest = await roleService.GetHighestGroupPosition(targetUserId, serverId).ConfigureAwait(false);
        if (moderatorHighest != int.MaxValue && targetHighest >= moderatorHighest)
            return Error.Forbidden("ROLE_HIERARCHY", $"You cannot {action} a member with an equal or higher role position");

        return server;
    }
}
