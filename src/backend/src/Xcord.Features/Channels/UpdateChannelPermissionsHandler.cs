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

public sealed record UpdateChannelPermissionsRequest(
    string SubjectId,
    Dictionary<string, string> Permissions  // key -> "Allow" | "Deny" | "Inherit"
);

public sealed record UpdateChannelPermissionsCommand(
    long ServerId,
    long ChannelId,
    string SubjectId,
    Dictionary<string, string> Permissions
);

/// <summary>
/// PUT /api/v1/servers/{serverId}/channels/{channelId}/permissions
///
/// Upserts a channel permission override for a role or member.
/// The subject is identified by SubjectId (Snowflake string).
/// If all permissions are "Inherit", the override row is deleted.
/// Returns the full updated permissions list (same shape as GET).
/// Requires ManageChannels server permission.
/// </summary>
public sealed class UpdateChannelPermissionsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    SnowflakeIdGenerator snowflakeGenerator)
    : IRequestHandler<UpdateChannelPermissionsCommand, Result<ChannelPermissionsResponseDto>>
{
    private static readonly (string Key, Permission Bit)[] PermissionMap =
    [
        ("ViewChannel",      Permission.ViewChannels),
        ("SendMessages",     Permission.SendMessages),
        ("ManageMessages",   Permission.ManageMessages),
        ("AttachFiles",      Permission.AttachFiles),
        ("EmbedLinks",       Permission.EmbedLinks),
        ("MentionEveryone",  Permission.MentionEveryone),
        ("ManageChannel",    Permission.ManageChannels),
        ("Connect",          Permission.Connect),
        ("Speak",            Permission.Speak),
    ];

    public async Task<Result<ChannelPermissionsResponseDto>> Handle(
        UpdateChannelPermissionsCommand request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Require ManageChannels on the server
        var permCheck = await permissionService.EnsureServerPermission(
            userId, request.ServerId, Permission.ManageChannels);

        if (permCheck.IsFailure) return permCheck.Error;

        // Verify channel belongs to this server
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId && c.ServerId == request.ServerId, cancellationToken);

        if (channel == null)
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");

        // Parse the subjectId
        if (!long.TryParse(request.SubjectId, out var targetId))
            return Error.Validation("INVALID_SUBJECT_ID", "SubjectId must be a valid snowflake ID");

        // Determine target type: if targetId is a role in this server, it's a Role override; otherwise User
        var isRole = await dbContext.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == targetId && r.ServerId == request.ServerId, cancellationToken);

        var targetType = isRole ? OverrideTargetType.Role : OverrideTargetType.User;

        // Compute allow/deny bitfields from the permissions dict
        long allow = 0L;
        long deny = 0L;

        foreach (var (key, bit) in PermissionMap)
        {
            if (!request.Permissions.TryGetValue(key, out var state)) continue;

            switch (state)
            {
                case "Allow":
                    allow |= (long)bit;
                    break;
                case "Deny":
                    deny |= (long)bit;
                    break;
                // "Inherit" — neither bit set
            }
        }

        // Upsert the override
        var existing = await dbContext.ChannelPermissionOverrides
            .FirstOrDefaultAsync(o =>
                o.ChannelId == request.ChannelId &&
                o.TargetType == targetType &&
                o.TargetId == targetId,
                cancellationToken);

        if (allow == 0L && deny == 0L)
        {
            // All permissions are "Inherit" — delete the override if it exists
            if (existing != null)
            {
                dbContext.ChannelPermissionOverrides.Remove(existing);
            }
        }
        else if (existing == null)
        {
            var newOverride = new ChannelPermissionOverride
            {
                Id = snowflakeGenerator.NextId(),
                ChannelId = request.ChannelId,
                TargetType = targetType,
                TargetId = targetId,
                Allow = allow,
                Deny = deny,
            };
            dbContext.ChannelPermissionOverrides.Add(newOverride);
        }
        else
        {
            existing.Allow = allow;
            existing.Deny = deny;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate permission caches for all members affected by this override
        if (targetType == OverrideTargetType.Role)
        {
            await permissionService.InvalidateRoleMembersPermissionsAsync(targetId, request.ServerId, cancellationToken);
        }
        else
        {
            await permissionService.InvalidateUserPermissionsAsync(targetId, request.ServerId, cancellationToken);
        }

        // Return the refreshed permissions list (delegate to the GET handler logic via shared helper)
        var getQuery = new GetChannelPermissionsQuery(request.ServerId, request.ChannelId);
        var getHandler = new GetChannelPermissionsHandler(dbContext, currentUserService, permissionService);
        return await getHandler.Handle(getQuery, cancellationToken);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPut(
            "/api/v1/servers/{serverId}/channels/{channelId}/permissions",
            async (
                [FromRoute] long serverId,
                [FromRoute] long channelId,
                [FromBody] UpdateChannelPermissionsRequest request,
                IRequestHandler<UpdateChannelPermissionsCommand, Result<ChannelPermissionsResponseDto>> handler,
                CancellationToken ct) =>
            {
                var command = new UpdateChannelPermissionsCommand(
                    ServerId: serverId,
                    ChannelId: channelId,
                    SubjectId: request.SubjectId,
                    Permissions: request.Permissions);

                return await handler.ExecuteAsync(command, ct);
            })
            .RequireAnyAuthorization(Policies.User)
            .WithTags("Channels")
            .WithName("UpdateChannelPermissions")
            .Produces<ChannelPermissionsResponseDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
