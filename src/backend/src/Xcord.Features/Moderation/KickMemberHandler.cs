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
    INotificationService notificationService,
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

        // Member removal and counter decrement must commit together.
        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Remove server member
        dbContext.ServerMembers.Remove(serverMember);

        // Create audit log
        dbContext.AuditLogs.AddEntry(snowflakeGenerator, request.ServerId, moderatorId, "MemberKick", request.UserId, request.Reason, now);

        // Add system message (MemberKick) in the server's system channel if configured
        var systemMsg = await dbContext.AddModerationSystemMessage(
            snowflakeGenerator, server,
            request.UserId, moderatorId, MessageType.MemberKick, request.Reason, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Atomically decrement Server.MemberCount with a SQL UPDATE rather than
        // read-then-write, matching BanMemberHandler, so concurrent membership
        // changes never lose updates.
        await dbContext.Servers
            .Where(s => s.Id == request.ServerId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.MemberCount, s => s.MemberCount - 1),
                cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // Notify after save
        await notificationService.NotifyServerAsync(request.ServerId, "Member_Kicked", new
        {
            ServerId = request.ServerId,
            UserId = request.UserId,
            ModeratorId = moderatorId,
            Reason = request.Reason
        }, cancellationToken);

        if (systemMsg != null)
        {
            await notificationService.NotifyConversationAsync(systemMsg.ConversationId, "Chat_MessageCreated",
            Messages.MessageEventPayloads.ForSystemCreated(systemMsg.Message), cancellationToken);
        }

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

            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("KickMember")
        .WithTags("Moderation");
    }
}
