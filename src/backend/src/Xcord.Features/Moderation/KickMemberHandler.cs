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

        // Cannot kick yourself
        if (moderatorId == request.UserId)
        {
            return Error.Validation("CANNOT_KICK_SELF", "You cannot kick yourself");
        }

        // Verify target user is a member of the server
        var serverMember = await dbContext.ServerMembers
            .FirstOrDefaultAsync(sm => sm.UserId == request.UserId && sm.ServerId == request.ServerId, cancellationToken);

        if (serverMember == null)
        {
            return Error.NotFound("MEMBER_NOT_FOUND", "User is not a member of this server");
        }

        var server = await dbContext.Servers
            .FirstOrDefaultAsync(s => s.Id == request.ServerId, cancellationToken);

        if (server == null)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Cannot kick the server owner
        if (server.OwnerId == request.UserId)
        {
            return Error.Validation("CANNOT_KICK_OWNER", "You cannot kick the server owner");
        }

        // Role hierarchy check: cannot kick a user with equal or higher role position
        var moderatorHighest = await roleService.GetHighestGroupPosition(moderatorId, request.ServerId);
        var targetHighest = await roleService.GetHighestGroupPosition(request.UserId, request.ServerId);
        if (moderatorHighest != int.MaxValue && targetHighest >= moderatorHighest)
        {
            return Error.Forbidden("ROLE_HIERARCHY",
                "You cannot kick a member with an equal or higher role position");
        }

        var now = DateTimeOffset.UtcNow;

        // Remove server member
        dbContext.ServerMembers.Remove(serverMember);

        // Decrement server member count
        server.MemberCount--;

        // Create audit log
        var auditLogId = snowflakeGenerator.NextId();
        var auditLog = new AuditLog
        {
            Id = auditLogId,
            ServerId = request.ServerId,
            ActorId = moderatorId,
            ActionType = "MemberKick",
            TargetId = request.UserId,
            Reason = request.Reason,
            CreatedAt = now
        };

        dbContext.AuditLogs.Add(auditLog);

        // Write outbox event
        await outboxWriter.WriteAsync(dbContext, "Member.Kicked", new
        {
            ServerId = request.ServerId,
            UserId = request.UserId,
            ModeratorId = moderatorId,
            Reason = request.Reason
        }, cancellationToken);

        // Create system message (MemberKick) in the server's system channel if configured
        if (server.SystemChannelId.HasValue)
        {
            var systemChannel = await dbContext.Channels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == server.SystemChannelId.Value, cancellationToken);

            if (systemChannel != null)
            {
                var kickedUser = await dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

                var systemMessageId = snowflakeGenerator.NextId();
                var systemMessage = new Message
                {
                    Id = systemMessageId,
                    ConversationId = systemChannel.ConversationId,
                    AuthorId = null,
                    Type = MessageType.MemberKick,
                    Content = string.Empty,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        UserId = request.UserId.ToString(),
                        Username = kickedUser?.Username ?? string.Empty,
                        DisplayName = kickedUser?.DisplayName ?? string.Empty,
                        ModeratorId = moderatorId.ToString(),
                        Reason = request.Reason
                    }),
                    CreatedAt = now
                };

                dbContext.Messages.Add(systemMessage);

                await outboxWriter.WriteAsync(dbContext, "Message.Created", new
                {
                    MessageId = systemMessage.Id,
                    ConversationId = systemMessage.ConversationId,
                    AuthorId = (long?)null
                }, cancellationToken);
            }
        }

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
