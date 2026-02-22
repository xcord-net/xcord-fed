using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

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
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService,
    ITimeoutService timeoutService,
    ILogger<RemoveTimeoutHandler> logger)
    : IRequestHandler<RemoveTimeoutCommand, Result<RemoveTimeoutResponse>>
{
    public async Task<Result<RemoveTimeoutResponse>> Handle(RemoveTimeoutCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var moderatorId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Check if moderator has TimeoutMembers permission
        var permissionResult = await permissionService.EnsureServerPermission(
            moderatorId,
            request.ServerId,
            Permission.TimeoutMembers);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        var now = DateTimeOffset.UtcNow;

        // Remove from Redis cache
        await timeoutService.RemoveTimeoutAsync(request.UserId, request.ServerId, cancellationToken);

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
