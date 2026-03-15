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

public sealed record TimeoutMemberCommand(
    long ServerId,
    long UserId,
    int DurationMinutes,
    string? Reason
);

public sealed record TimeoutMemberResponse(
    long Id,
    long UserId,
    long ServerId,
    long? ModeratorId,
    DateTimeOffset ExpiresAt,
    string? Reason,
    DateTimeOffset CreatedAt
);

public sealed class TimeoutMemberHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ITimeoutService timeoutService,
    INotificationService notificationService,
    ILogger<TimeoutMemberHandler> logger)
    : IRequestHandler<TimeoutMemberCommand, Result<TimeoutMemberResponse>>, IValidatable<TimeoutMemberCommand>
{
    public Error? Validate(TimeoutMemberCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be greater than 0");
        }

        if (request.UserId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "UserId must be greater than 0");
        }

        if (request.DurationMinutes <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "DurationMinutes must be greater than 0");
        }

        if (request.DurationMinutes > 40320) // 28 days
        {
            return Error.Validation("VALIDATION_ERROR", "DurationMinutes cannot exceed 40320 (28 days)");
        }

        if (request.Reason != null && request.Reason.Length > 512)
        {
            return Error.Validation("VALIDATION_ERROR", "Reason cannot exceed 512 characters");
        }

        return null;
    }

    public async Task<Result<TimeoutMemberResponse>> Handle(TimeoutMemberCommand request, CancellationToken cancellationToken)
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

        // Verify target user is a member of the server
        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == request.UserId && sm.ServerId == request.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.NotFound("MEMBER_NOT_FOUND", "User is not a member of this server");
        }

        // Self-check, owner protection, and role hierarchy
        var validationResult = await dbContext.ValidateModerationTarget(
            roleService, moderatorId, request.UserId, request.ServerId, "timeout", cancellationToken);
        if (validationResult.IsFailure) return validationResult.Error;

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(request.DurationMinutes);

        // Create timeout
        var timeoutId = snowflakeGenerator.NextId();
        var timeout = new Entities.Timeout
        {
            Id = timeoutId,
            UserId = request.UserId,
            ServerId = request.ServerId,
            ModeratorId = moderatorId,
            ExpiresAt = expiresAt,
            Reason = request.Reason,
            CreatedAt = now
        };

        dbContext.Timeouts.Add(timeout);

        // Create audit log
        dbContext.AuditLogs.AddEntry(snowflakeGenerator, request.ServerId, moderatorId, "MemberTimeout", request.UserId, request.Reason, now);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Notify after save
        await notificationService.NotifyServerAsync(request.ServerId, "Notify_MemberTimedOut", new
        {
            ServerId = request.ServerId,
            UserId = request.UserId,
            ModeratorId = moderatorId,
            ExpiresAt = expiresAt,
            Reason = request.Reason
        });

        // Cache timeout in Redis
        await timeoutService.SetTimeoutAsync(request.UserId, request.ServerId, expiresAt, cancellationToken);

        logger.LogInformation(
            "Moderator {ModeratorId} timed out user {UserId} in server {ServerId} until {ExpiresAt}",
            moderatorId, request.UserId, request.ServerId, expiresAt);

        return new TimeoutMemberResponse(
            Id: timeout.Id,
            UserId: timeout.UserId,
            ServerId: timeout.ServerId,
            ModeratorId: timeout.ModeratorId,
            ExpiresAt: timeout.ExpiresAt,
            Reason: timeout.Reason,
            CreatedAt: timeout.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/members/{userId}/timeout", async (
            long serverId,
            long userId,
            TimeoutMemberRequest requestBody,
            IRequestHandler<TimeoutMemberCommand, Result<TimeoutMemberResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new TimeoutMemberCommand(
                ServerId: serverId,
                UserId: userId,
                DurationMinutes: requestBody.DurationMinutes,
                Reason: requestBody.Reason
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("TimeoutMember")
        .WithTags("Moderation");
    }
}

public sealed record TimeoutMemberRequest(
    int DurationMinutes,
    string? Reason
);
