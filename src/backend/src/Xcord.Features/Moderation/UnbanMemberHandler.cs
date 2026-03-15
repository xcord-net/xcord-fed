using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Moderation;

public sealed record UnbanMemberCommand(
    long ServerId,
    long UserId
);

public sealed record UnbanMemberResponse(
    bool Success
);

public sealed class UnbanMemberHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<UnbanMemberHandler> logger)
    : IRequestHandler<UnbanMemberCommand, Result<UnbanMemberResponse>>
{
    public async Task<Result<UnbanMemberResponse>> Handle(UnbanMemberCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var moderatorId = userIdResult.Value;

        // Check if moderator has BanMembers permission
        var permissionResult = await roleService.EnsureServerRole(
            moderatorId,
            request.ServerId,
            Role.BanMembers);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Find the ban
        var ban = await dbContext.Bans
            .FirstOrDefaultAsync(b => b.UserId == request.UserId && b.ServerId == request.ServerId, cancellationToken);

        if (ban == null)
        {
            return Error.NotFound("BAN_NOT_FOUND", "User is not banned from this server");
        }

        var now = DateTimeOffset.UtcNow;

        // Soft-delete the ban
        ban.DeletedAt = now;

        // Create audit log
        dbContext.AuditLogs.AddEntry(
            snowflakeGenerator,
            serverId: request.ServerId,
            actorId: moderatorId,
            actionType: "MemberUnban",
            targetId: request.UserId,
            reason: null,
            createdAt: now);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Moderator {ModeratorId} unbanned user {UserId} from server {ServerId}",
            moderatorId, request.UserId, request.ServerId);

        return new UnbanMemberResponse(Success: true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/bans/{userId}", async (
            long serverId,
            long userId,
            IRequestHandler<UnbanMemberCommand, Result<UnbanMemberResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new UnbanMemberCommand(
                ServerId: serverId,
                UserId: userId
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UnbanMember")
        .WithTags("Moderation");
    }
}
