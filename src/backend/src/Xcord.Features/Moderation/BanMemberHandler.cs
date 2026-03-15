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

public sealed record BanMemberCommand(
    long ServerId,
    long UserId,
    string? Reason,
    int? DeleteMessageDays
);

public sealed record BanMemberResponse(
    long Id,
    long UserId,
    long ServerId,
    long? ModeratorId,
    string? Reason,
    int? DeleteMessageDays,
    DateTimeOffset CreatedAt
);

public sealed class BanMemberHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    IOutboxWriter outboxWriter,
    ILogger<BanMemberHandler> logger)
    : IRequestHandler<BanMemberCommand, Result<BanMemberResponse>>, IValidatable<BanMemberCommand>
{
    public Error? Validate(BanMemberCommand request)
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

        if (request.DeleteMessageDays.HasValue && (request.DeleteMessageDays.Value < 1 || request.DeleteMessageDays.Value > 7))
        {
            return Error.Validation("VALIDATION_ERROR", "DeleteMessageDays must be between 1 and 7");
        }

        return null;
    }

    public async Task<Result<BanMemberResponse>> Handle(BanMemberCommand request, CancellationToken cancellationToken)
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

        // Verify target user is a member of the server
        var serverMember = await dbContext.ServerMembers
            .FirstOrDefaultAsync(sm => sm.UserId == request.UserId && sm.ServerId == request.ServerId, cancellationToken);

        if (serverMember == null)
        {
            return Error.NotFound("MEMBER_NOT_FOUND", "User is not a member of this server");
        }

        // Self-check, owner protection, and role hierarchy
        var validationResult = await dbContext.ValidateModerationTarget(
            roleService, moderatorId, request.UserId, request.ServerId, "ban", cancellationToken);
        if (validationResult.IsFailure) return validationResult.Error;

        // Check if already banned
        var existingBan = await dbContext.Bans
            .AsNoTracking()
            .AnyAsync(b => b.UserId == request.UserId && b.ServerId == request.ServerId, cancellationToken);

        if (existingBan)
        {
            return Error.Conflict("ALREADY_BANNED", "User is already banned from this server");
        }

        var now = DateTimeOffset.UtcNow;

        // Create ban
        var banId = snowflakeGenerator.NextId();
        var ban = new Ban
        {
            Id = banId,
            UserId = request.UserId,
            ServerId = request.ServerId,
            ModeratorId = moderatorId,
            Reason = request.Reason,
            DeleteMessageDays = request.DeleteMessageDays,
            CreatedAt = now
        };

        dbContext.Bans.Add(ban);

        // Remove server member
        dbContext.ServerMembers.Remove(serverMember);

        // Decrement server member count
        var server = validationResult.Value;
        server.MemberCount--;

        // Optionally bulk soft-delete messages
        if (request.DeleteMessageDays.HasValue && request.DeleteMessageDays.Value > 0)
        {
            var cutoffDate = now.AddDays(-request.DeleteMessageDays.Value);

            // Get all channel conversations for this server
            var channelConversationIds = await dbContext.Channels
                .Where(c => c.ServerId == request.ServerId)
                .Select(c => c.ConversationId)
                .ToListAsync(cancellationToken);

            // Soft-delete messages from the banned user in these conversations
            var messagesToDelete = await dbContext.Messages
                .Where(m => m.AuthorId == request.UserId
                    && channelConversationIds.Contains(m.ConversationId)
                    && m.CreatedAt >= cutoffDate)
                .ToListAsync(cancellationToken);

            messagesToDelete.ForEach(message => message.DeletedAt = now);
        }

        // Create audit log
        dbContext.AuditLogs.AddEntry(snowflakeGenerator, request.ServerId, moderatorId, "MemberBan", request.UserId, request.Reason, now);

        // Write outbox event
        await outboxWriter.WriteAsync(dbContext, "Member.Banned", new
        {
            ServerId = request.ServerId,
            UserId = request.UserId,
            ModeratorId = moderatorId,
            Reason = request.Reason
        }, cancellationToken);

        // Create system message (MemberBan) in the server's system channel if configured
        await dbContext.SendModerationSystemMessage(
            snowflakeGenerator, outboxWriter, server,
            request.UserId, moderatorId, MessageType.MemberBan, request.Reason, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Moderator {ModeratorId} banned user {UserId} from server {ServerId}",
            moderatorId, request.UserId, request.ServerId);

        return new BanMemberResponse(
            Id: ban.Id,
            UserId: ban.UserId,
            ServerId: ban.ServerId,
            ModeratorId: ban.ModeratorId,
            Reason: ban.Reason,
            DeleteMessageDays: ban.DeleteMessageDays,
            CreatedAt: ban.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/bans", async (
            long serverId,
            BanMemberRequest requestBody,
            IRequestHandler<BanMemberCommand, Result<BanMemberResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new BanMemberCommand(
                ServerId: serverId,
                UserId: requestBody.UserId,
                Reason: requestBody.Reason,
                DeleteMessageDays: requestBody.DeleteMessageDays
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("BanMember")
        .WithTags("Moderation");
    }
}

public sealed record BanMemberRequest(
    long UserId,
    string? Reason,
    int? DeleteMessageDays
);
