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
    INotificationService notificationService,
    ICurrentUserService currentUserService,
    ILogger<JoinByInviteHandler> logger)
    : IRequestHandler<JoinByInviteCommand, Result<JoinByInviteResponse>>
{
    public async Task<Result<JoinByInviteResponse>> Handle(JoinByInviteCommand request, CancellationToken cancellationToken)
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
            // users join a 1-use link without decrementing the counter). The increment
            // is conditional SQL so concurrent consumers can never push Uses past
            // MaxUses; an exhausted invite is fine here since no new member is added.
            await ConsumeInviteUseAsync(invite.Code, cancellationToken).ConfigureAwait(false);

            return new JoinByInviteResponse(
                Id: invite.Server.Id,
                Name: invite.Server.Name,
                Description: invite.Server.Description,
                IconUrl: invite.Server.IconUrl,
                BannerUrl: invite.Server.BannerUrl,
                OwnerId: invite.Server.OwnerId,
                MemberCount: invite.Server.MemberCount,
                PreferredLocale: invite.Server.PreferredLocale,
                CreatedAt: invite.Server.CreatedAt,
                ChannelId: invite.ChannelId
            );
        }

        var now = DateTimeOffset.UtcNow;

        // The invite consume, member insert, and counter increment must be atomic:
        // ExecuteUpdateAsync runs immediately, so without a transaction a failed
        // SaveChanges would burn an invite use with no membership to show for it.
        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Consume the invite use first with a conditional SQL increment. Checking
        // Uses < MaxUses inside the UPDATE closes the validation TOCTOU: of N
        // concurrent joiners on a 1-use invite, exactly one row update succeeds.
        if (!await ConsumeInviteUseAsync(invite.Code, cancellationToken).ConfigureAwait(false))
        {
            return Error.Validation("INVITE_MAX_USES_REACHED", "This invite has reached its maximum number of uses");
        }

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
            .Where(c => c.ServerId == invite.ServerId && c.Capabilities.HasFlag(ChannelCapability.Chat))
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

        // Increment server member count atomically (single SQL UPDATE, same
        // pattern as BanMemberHandler) so concurrent joins/leaves never lose updates.
        await dbContext.Servers
            .Where(s => s.Id == invite.ServerId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.MemberCount, s => s.MemberCount + 1),
                cancellationToken).ConfigureAwait(false);

        // Create system message (MemberJoin) in the server's system channel if configured
        long? systemMessageConversationId = null;
        long? systemMessageId = null;
        Message? systemMessageEntity = null;
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
                systemMessageConversationId = systemChannel.ConversationId;
                systemMessageId = messageId;
                systemMessageEntity = systemMessage;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // The tracked Server entity predates the atomic increment; fetch the
        // committed count for the response instead of reporting a stale value.
        var memberCount = await dbContext.Servers
            .AsNoTracking()
            .Where(s => s.Id == invite.ServerId)
            .Select(s => s.MemberCount)
            .FirstAsync(cancellationToken).ConfigureAwait(false);

        // Notify after save
        if (systemMessageConversationId.HasValue && systemMessageId.HasValue)
        {
            await notificationService.NotifyConversationAsync(systemMessageConversationId.Value, "Chat_MessageCreated",
            Messages.MessageEventPayloads.ForSystemCreated(systemMessageEntity!), cancellationToken);
        }

        await notificationService.NotifyServerAsync(invite.ServerId, "Member_Joined", new
        {
            ServerId = invite.ServerId,
            UserId = userId
        }, cancellationToken);

        logger.LogInformation(
            "User {UserId} joined server {ServerId} via invite {InviteCode}",
            userId, invite.ServerId, request.InviteCode);

        return new JoinByInviteResponse(
            Id: invite.Server.Id,
            Name: invite.Server.Name,
            Description: invite.Server.Description,
            IconUrl: invite.Server.IconUrl,
            BannerUrl: invite.Server.BannerUrl,
            OwnerId: invite.Server.OwnerId,
            MemberCount: memberCount,
            PreferredLocale: invite.Server.PreferredLocale,
            CreatedAt: invite.Server.CreatedAt,
            ChannelId: invite.ChannelId
        );
    }

    /// <summary>
    /// Consumes one use of the invite with a conditional atomic UPDATE.
    /// Returns false when the invite was already at MaxUses, so concurrent
    /// joiners cannot push Uses past the cap.
    /// </summary>
    private async Task<bool> ConsumeInviteUseAsync(string inviteCode, CancellationToken cancellationToken)
    {
        var updated = await dbContext.Invites
            .Where(i => i.Code == inviteCode && (i.MaxUses == null || i.Uses < i.MaxUses.Value))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.Uses, i => i.Uses + 1),
                cancellationToken).ConfigureAwait(false);

        return updated > 0;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/invites/{code}/accept", async (
            string code,
            IRequestHandler<JoinByInviteCommand, Result<JoinByInviteResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new JoinByInviteCommand(code);
            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("JoinByInvite")
        .WithTags("Invites");
    }
}
