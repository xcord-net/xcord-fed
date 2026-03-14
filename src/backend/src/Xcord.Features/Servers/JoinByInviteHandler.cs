using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Servers;

public sealed record JoinByInviteCommand(string InviteCode);

public sealed class JoinByInviteHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IOutboxWriter outboxWriter,
    ICurrentUserService currentUserService,
    ILogger<JoinByInviteHandler> logger)
    : IRequestHandler<JoinByInviteCommand, Result<ServerDto>>
{
    public async Task<Result<ServerDto>> Handle(JoinByInviteCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Find invite with server
        var invite = await dbContext.Invites
            .Include(i => i.Server)
            .Include(i => i.Group)
            .FirstOrDefaultAsync(i => i.Code == request.InviteCode, cancellationToken);

        if (invite == null)
        {
            return Error.NotFound("INVITE_NOT_FOUND", "Invite not found");
        }

        // Validate invite is not expired
        if (invite.ExpiresAt.HasValue && invite.ExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            return Error.Validation("INVITE_EXPIRED", "This invite has expired");
        }

        // Validate invite has not reached max uses
        if (invite.MaxUses.HasValue && invite.Uses >= invite.MaxUses.Value)
        {
            return Error.Validation("INVITE_MAX_USES_REACHED", "This invite has reached its maximum number of uses");
        }

        // Check if user is banned from the server
        var isBanned = await dbContext.Bans
            .AnyAsync(b => b.UserId == userId && b.ServerId == invite.ServerId, cancellationToken);

        if (isBanned)
        {
            return Error.Forbidden("BANNED", "You are banned from this server");
        }

        // Check if user is already a member
        var alreadyMember = await dbContext.ServerMembers
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == invite.ServerId, cancellationToken);

        if (alreadyMember)
        {
            // User is already a member - consume the invite use so max-uses accounting
            // remains correct (otherwise a member re-clicking an invite would let extra
            // users join a 1-use link without decrementing the counter).
            invite.Uses++;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new ServerDto(
                Id: invite.Server.Id,
                Name: invite.Server.Name,
                Description: invite.Server.Description,
                IconUrl: invite.Server.IconUrl,
                BannerUrl: invite.Server.BannerUrl,
                OwnerId: invite.Server.OwnerId,
                MemberCount: invite.Server.MemberCount,
                PreferredLocale: invite.Server.PreferredLocale,
                CreatedAt: invite.Server.CreatedAt
            );
        }

        var now = DateTimeOffset.UtcNow;

        // Create ServerMember
        var serverMember = new ServerMember
        {
            UserId = userId,
            ServerId = invite.ServerId,
            JoinedAt = now
        };

        dbContext.ServerMembers.Add(serverMember);

        // Auto-assign group from invite if configured
        if (invite.GroupId.HasValue)
        {
            var existingAssignment = await dbContext.MemberGroups
                .AnyAsync(mg => mg.UserId == userId && mg.ServerId == invite.ServerId && mg.GroupId == invite.GroupId.Value, cancellationToken);

            if (!existingAssignment)
            {
                dbContext.MemberGroups.Add(new MemberGroup
                {
                    UserId = userId,
                    ServerId = invite.ServerId,
                    GroupId = invite.GroupId.Value
                });
            }
        }

        // Create ReadState rows for all text/forum channels in the server so the
        // unread notification system can track messages the new member hasn't seen.
        // Skip any that already exist (e.g. user rejoining after a ban - ReadState rows
        // are not deleted when a member is banned, so they may still be present).
        var channelConversationIds = await dbContext.Channels
            .AsNoTracking()
            .Where(c => c.ServerId == invite.ServerId && c.Type != ChannelType.Voice)
            .Select(c => c.ConversationId)
            .ToListAsync(cancellationToken);

        var existingReadStateConversationIds = await dbContext.ReadStates
            .AsNoTracking()
            .Where(rs => rs.UserId == userId && channelConversationIds.Contains(rs.ConversationId))
            .Select(rs => rs.ConversationId)
            .ToHashSetAsync(cancellationToken);

        foreach (var conversationId in channelConversationIds)
        {
            if (existingReadStateConversationIds.Contains(conversationId))
                continue;

            dbContext.ReadStates.Add(new Xcord.Entities.ReadState
            {
                UserId = userId,
                ConversationId = conversationId,
                UnreadCount = 0,
                MentionCount = 0
            });
        }

        // Increment invite uses
        invite.Uses++;

        // Increment server member count atomically
        invite.Server.MemberCount++;

        // Create system message (MemberJoin) in the server's system channel if configured
        if (invite.Server.SystemChannelId.HasValue)
        {
            var systemChannel = await dbContext.Channels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == invite.Server.SystemChannelId.Value, cancellationToken);

            if (systemChannel != null)
            {
                var user = await dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

                var messageId = snowflakeGenerator.NextId();
                var systemMessage = new Message
                {
                    Id = messageId,
                    ConversationId = systemChannel.ConversationId,
                    AuthorId = null,
                    Type = MessageType.MemberJoin,
                    Content = string.Empty,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        UserId = userId.ToString(),
                        Username = user?.Username ?? string.Empty,
                        DisplayName = user?.DisplayName ?? string.Empty
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

        // Write Member.Joined outbox event (used by outgoing webhooks)
        await outboxWriter.WriteAsync(dbContext, "Member.Joined", new
        {
            ServerId = invite.ServerId,
            UserId = userId
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} joined server {ServerId} via invite {InviteCode}",
            userId, invite.ServerId, request.InviteCode);

        return new ServerDto(
            Id: invite.Server.Id,
            Name: invite.Server.Name,
            Description: invite.Server.Description,
            IconUrl: invite.Server.IconUrl,
            BannerUrl: invite.Server.BannerUrl,
            OwnerId: invite.Server.OwnerId,
            MemberCount: invite.Server.MemberCount,
            PreferredLocale: invite.Server.PreferredLocale,
            CreatedAt: invite.Server.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/invites/{code}/accept", async (
            string code,
            IRequestHandler<JoinByInviteCommand, Result<ServerDto>> handler,
            CancellationToken ct) =>
        {
            var command = new JoinByInviteCommand(code);
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("JoinByInvite")
        .WithTags("Invites");
    }
}
