using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

// DTO shapes that mirror ChannelPermissions.tsx types on the frontend

public sealed record PermissionOverrideDto(
    string SubjectType,   // "Group" | "Member"
    string SubjectId,     // Snowflake as string
    string SubjectName,
    Dictionary<string, string> Permissions // key -> "Allow" | "Deny" | "Inherit"
);

public sealed record ChannelPermissionsResponseDto(
    List<PermissionOverrideDto> Overrides
);

public sealed record GetChannelPermissionsQuery(long ServerId, long ChannelId);

/// <summary>
/// GET /api/v1/servers/{serverId}/channels/{channelId}/permissions
///
/// Returns all channel permission overrides for a channel.
/// Requires ManageChannels server permission.
/// </summary>
public sealed class GetChannelPermissionsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService)
    : IRequestHandler<GetChannelPermissionsQuery, Result<ChannelPermissionsResponseDto>>
{
    // Role keys exposed to the frontend (matches PermissionKey in ChannelPermissions.tsx)
    private static readonly (string Key, Role Bit)[] PermissionMap =
    [
        ("ViewChannel",      Role.ViewChannels),
        ("SendMessages",     Role.SendMessages),
        ("ManageMessages",   Role.ManageMessages),
        ("AttachFiles",      Role.AttachFiles),
        ("EmbedLinks",       Role.EmbedLinks),
        ("MentionEveryone",  Role.MentionEveryone),
        ("ManageChannel",    Role.ManageChannels),
        ("Connect",          Role.Connect),
        ("Speak",            Role.Speak),
    ];

    public async Task<Result<ChannelPermissionsResponseDto>> Handle(
        GetChannelPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Require ManageChannels on the server
        var permCheck = await roleService.EnsureServerRole(
            userId, request.ServerId, Role.ManageChannels);

        if (permCheck.IsFailure) return permCheck.Error;

        // Verify channel belongs to this server
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId && c.ServerId == request.ServerId, cancellationToken);

        if (channel == null)
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");

        // Load all groups in the server (for name lookup)
        var groups = await dbContext.Groups
            .AsNoTracking()
            .Where(g => g.ServerId == request.ServerId)
            .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);

        // Load all members in the server (for name lookup when subject is a user)
        var members = await dbContext.ServerMembers
            .AsNoTracking()
            .Where(m => m.ServerId == request.ServerId)
            .Join(dbContext.Users,
                  m => m.UserId,
                  u => u.Id,
                  (m, u) => new { m.UserId, u.Username })
            .ToDictionaryAsync(x => x.UserId, x => x.Username, cancellationToken);

        // Load existing overrides
        var overrides = await dbContext.ChannelPermissionOverrides
            .AsNoTracking()
            .Where(o => o.ChannelId == request.ChannelId)
            .ToListAsync(cancellationToken);

        // Build a response override for every group in the server (always show all groups, even without overrides)
        // This ensures the UI can always display and configure any group.
        var result = new List<PermissionOverrideDto>();

        foreach (var (groupId, groupName) in groups.OrderBy(kvp => kvp.Value))
        {
            var existingOverride = overrides.FirstOrDefault(
                o => o.TargetType == OverrideTargetType.Group && o.TargetId == groupId);

            result.Add(BuildOverrideDto(
                subjectType: "Group",
                subjectId: groupId.ToString(),
                subjectName: groupName,
                existingOverride));
        }

        // Add member-specific overrides
        foreach (var memberOverride in overrides.Where(o => o.TargetType == OverrideTargetType.User))
        {
            var memberName = members.TryGetValue(memberOverride.TargetId, out var name) ? name : memberOverride.TargetId.ToString();
            result.Add(BuildOverrideDto(
                subjectType: "Member",
                subjectId: memberOverride.TargetId.ToString(),
                subjectName: memberName,
                memberOverride));
        }

        return new ChannelPermissionsResponseDto(result);
    }

    private static PermissionOverrideDto BuildOverrideDto(
        string subjectType,
        string subjectId,
        string subjectName,
        ChannelPermissionOverride? existingOverride)
    {
        var permissions = new Dictionary<string, string>();

        foreach (var (key, bit) in PermissionMap)
        {
            string state;
            if (existingOverride == null)
            {
                state = "Inherit";
            }
            else
            {
                long allowBit = (long)bit;
                long denyBit = (long)bit;

                if ((existingOverride.Deny & denyBit) != 0)
                    state = "Deny";
                else if ((existingOverride.Allow & allowBit) != 0)
                    state = "Allow";
                else
                    state = "Inherit";
            }

            permissions[key] = state;
        }

        return new PermissionOverrideDto(subjectType, subjectId, subjectName, permissions);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet(
            "/api/v1/servers/{serverId}/channels/{channelId}/permissions",
            async (
                [FromRoute] long serverId,
                [FromRoute] long channelId,
                IRequestHandler<GetChannelPermissionsQuery, Result<ChannelPermissionsResponseDto>> handler,
                CancellationToken ct) =>
            {
                var query = new GetChannelPermissionsQuery(serverId, channelId);
                return await handler.ExecuteAsync(query, ct);
            })
            .RequireAnyAuthorization(Policies.User)
            .WithTags("Channels")
            .WithName("GetChannelPermissions")
            .Produces<ChannelPermissionsResponseDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
