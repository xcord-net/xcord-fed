using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Infrastructure.Services;
using Xcord.Infrastructure.Data;
using Xcord;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for resolving conversations and verifying membership/permissions.
/// </summary>
public sealed class ConversationResolver : IConversationResolver
{
    private readonly AppDbContext _dbContext;
    private readonly IRoleService _roleService;

    public ConversationResolver(
        AppDbContext dbContext,
        IRoleService roleService)
    {
        _dbContext = dbContext;
        _roleService = roleService;
    }

    public async Task<Result<ConversationContext>> ResolveAsync(
        long conversationId,
        long userId,
        Role? requiredRole = null,
        CancellationToken cancellationToken = default)
    {
        // Get conversation
        var conversation = await _dbContext.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation == null)
        {
            return Error.NotFound("CONVERSATION_NOT_FOUND", "Conversation not found");
        }

        long serverId = 0;
        long channelId = 0;

        if (conversation.Type == ConversationType.DmChannel)
        {
            // For DM conversations, verify user is a member
            var dmChannel = await _dbContext.DmChannels
                .AsNoTracking()
                .Include(dm => dm.Members)
                .FirstOrDefaultAsync(dm => dm.ConversationId == conversationId, cancellationToken);

            if (dmChannel == null)
            {
                return Error.NotFound("DM_NOT_FOUND", "DM channel not found");
            }

            // Verify current user is a member
            var isMember = dmChannel.Members.Any(m => m.UserId == userId);
            if (!isMember)
            {
                return Error.Forbidden("NOT_MEMBER", "You are not a member of this DM channel");
            }

            // No server permission checks for DMs
        }
        else if (conversation.Type == ConversationType.Channel)
        {
            // Get the channel to resolve server ID
            var channel = await _dbContext.Channels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ConversationId == conversationId, cancellationToken);

            if (channel == null)
            {
                return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
            }

            serverId = channel.ServerId;
            channelId = channel.Id;

            // Verify user is a member of the server
            var isMember = await _dbContext.ServerMembers
                .AsNoTracking()
                .AnyAsync(sm => sm.UserId == userId && sm.ServerId == serverId, cancellationToken);

            if (!isMember)
            {
                return Error.Forbidden("NOT_MEMBER", "User is not a member of this server");
            }

            // Check permission if required
            if (requiredRole.HasValue)
            {
                var permissionResult = await _roleService.EnsureChannelRole(
                    userId,
                    channelId,
                    requiredRole.Value);

                if (permissionResult.IsFailure)
                {
                    return permissionResult.Error;
                }
            }
        }
        else if (conversation.Type == ConversationType.Thread)
        {
            // Get the thread to resolve channel and server
            var thread = await _dbContext.Threads
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ConversationId == conversationId, cancellationToken);

            if (thread == null)
            {
                return Error.NotFound("THREAD_NOT_FOUND", "Thread not found");
            }

            // Get the parent channel
            var channel = await _dbContext.Channels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == thread.ChannelId, cancellationToken);

            if (channel == null)
            {
                return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
            }

            serverId = channel.ServerId;
            channelId = channel.Id;

            // Verify user is a member of the server
            var isMember = await _dbContext.ServerMembers
                .AsNoTracking()
                .AnyAsync(sm => sm.UserId == userId && sm.ServerId == serverId, cancellationToken);

            if (!isMember)
            {
                return Error.Forbidden("NOT_MEMBER", "User is not a member of this server");
            }

            // Check if thread is locked - only users with ManageMessages can interact with locked threads
            if (thread.IsLocked)
            {
                var channelPerms = await _roleService.GetChannelRoles(userId, channelId);
                var hasManagePermission = (channelPerms & (long)Role.ManageMessages) != 0;

                if (!hasManagePermission)
                {
                    return Error.Forbidden("THREAD_LOCKED", "This thread is locked");
                }
            }

            // Check permission if required
            if (requiredRole.HasValue)
            {
                var permissionResult = await _roleService.EnsureChannelRole(
                    userId,
                    channelId,
                    requiredRole.Value);

                if (permissionResult.IsFailure)
                {
                    return permissionResult.Error;
                }
            }
        }
        else
        {
            return Error.Validation("UNSUPPORTED_CONVERSATION_TYPE", "Unsupported conversation type");
        }

        return new ConversationContext(conversationId, conversation.Type, serverId, channelId);
    }
}
