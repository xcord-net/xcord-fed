using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Moderation;

public sealed record KickMemberCommand(
    long ServerId,
    long UserId,
    string? Reason
);

public sealed record KickMemberResponse(
    long ServerId,
    long UserId,
    long ModeratorId,
    string? Reason
);

public sealed class KickMemberHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    IOutboxWriter outboxWriter,
    ILogger<KickMemberHandler> logger)
    : IRequestHandler<KickMemberCommand, Result<KickMemberResponse>>, IValidatable<KickMemberCommand>
{
    public Error? Validate(KickMemberCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be greater than 0");
        }

        if (request.UserId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "UserId must be greater than 0");
        }

        if (request.Reason != null && request.Reason.Length > 512)
        {
            return Error.Validation("VALIDATION_ERROR", "Reason cannot exceed 512 characters");
        }

        return null;
    }

    public async Task<Result<KickMemberResponse>> Handle(KickMemberCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var moderatorId = userIdResult.Value;

        // Check if moderator has KickMembers permission
        var permissionResult = await roleService.EnsureServerRole(
            moderatorId,
            request.ServerId,
            Role.KickMembers);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Verify target user is a member of the server
        var serverMember = await dbContext.ServerMembers
            .FirstOrDefaultAsync(sm => sm.UserId == request.UserId && sm.ServerId == request.ServerId, cancellationToken);

        if (serverMember == null)
        {
            return Error.NotFound("MEMBER_NOT_FOUND", "User is not a member of this server");
        }

        // Self-check, owner protection, and role hierarchy
        var validationResult = await dbContext.ValidateModerationTarget(
            roleService, moderatorId, request.UserId, request.ServerId, "kick", cancellationToken);
        if (validationResult.IsFailure) return validationResult.Error;
        var server = validationResult.Value;

        var now = DateTimeOffset.UtcNow;

        // Remove server member
        dbContext.ServerMembers.Remove(serverMember);

        // Decrement server member count
        server.MemberCount--;

        // Create audit log
        dbContext.AuditLogs.AddEntry(snowflakeGenerator, request.ServerId, moderatorId, "MemberKick", request.UserId, request.Reason, now);

        // Write outbox event
        await outboxWriter.WriteAsync(dbContext, "Member.Kicked", new
        {
            ServerId = request.ServerId,
            UserId = request.UserId,
            ModeratorId = moderatorId,
            Reason = request.Reason
        }, cancellationToken);

        // Create system message (MemberKick) in the server's system channel if configured
        await dbContext.SendModerationSystemMessage(
            snowflakeGenerator, outboxWriter, server,
            request.UserId, moderatorId, MessageType.MemberKick, request.Reason, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Moderator {ModeratorId} kicked user {UserId} from server {ServerId}",
            moderatorId, request.UserId, request.ServerId);

        return new KickMemberResponse(
            ServerId: request.ServerId,
            UserId: request.UserId,
            ModeratorId: moderatorId,
            Reason: request.Reason
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/members/{userId}", async (
            long serverId,
            long userId,
            IRequestHandler<KickMemberCommand, Result<KickMemberResponse>> handler,
            HttpContext context,
            CancellationToken ct) =>
        {
            var reason = context.Request.Query["reason"].FirstOrDefault();
            var command = new KickMemberCommand(
                ServerId: serverId,
                UserId: userId,
                Reason: reason
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("KickMember")
        .WithTags("Moderation");
    }
}
