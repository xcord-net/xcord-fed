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

public sealed record RemoveTimeoutCommand(
    long ServerId,
    long UserId
);

public sealed record RemoveTimeoutResponse(
    bool Success
);

public sealed class RemoveTimeoutHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ITimeoutService timeoutService,
    ILogger<RemoveTimeoutHandler> logger)
    : IRequestHandler<RemoveTimeoutCommand, Result<RemoveTimeoutResponse>>
{
    public async Task<Result<RemoveTimeoutResponse>> Handle(RemoveTimeoutCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var moderatorId = userIdResult.Value;

        // Check if moderator has TimeoutMembers permission
        var permissionResult = await roleService.EnsureServerRole(
            moderatorId,
            request.ServerId,
            Role.TimeoutMembers);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        var now = DateTimeOffset.UtcNow;

        // Remove from Redis cache
        await timeoutService.RemoveTimeoutAsync(request.UserId, request.ServerId, cancellationToken);

        // Expire any active timeout records in the database so that the fallback
        // DB check in IsTimedOutAsync no longer finds an active timeout for this user
        var activeTimeouts = await dbContext.Timeouts
            .Where(t => t.UserId == request.UserId && t.ServerId == request.ServerId && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var t in activeTimeouts)
        {
            t.ExpiresAt = now;
        }

        // Create audit log
        var auditLogId = snowflakeGenerator.NextId();
        var auditLog = new AuditLog
        {
            Id = auditLogId,
            ServerId = request.ServerId,
            ActorId = moderatorId,
            ActionType = "MemberUpdate",
            TargetId = request.UserId,
            CreatedAt = now
        };

        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Moderator {ModeratorId} removed timeout for user {UserId} in server {ServerId}",
            moderatorId, request.UserId, request.ServerId);

        return new RemoveTimeoutResponse(Success: true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/members/{userId}/timeout", async (
            long serverId,
            long userId,
            IRequestHandler<RemoveTimeoutCommand, Result<RemoveTimeoutResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new RemoveTimeoutCommand(
                ServerId: serverId,
                UserId: userId
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("RemoveTimeout")
        .WithTags("Moderation");
    }
}
