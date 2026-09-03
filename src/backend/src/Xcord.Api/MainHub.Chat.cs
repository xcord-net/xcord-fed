using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Role = Xcord.Entities.Role;
using Xcord.Infrastructure.Services;
using Xcord.Infrastructure.Data;

namespace Xcord.Api;

/// <summary>
/// Chat_* methods: conversation join/leave and typing indicators.
/// </summary>
public partial class MainHub
{
    public async Task JoinConversation(long conversationId)
    {
        if (conversationId <= 0)
        {
            throw new HubException("Invalid id");
        }

        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        _logger.LogDebug("User {UserId} attempting to join conversation {ConversationId}", userId, conversationId);

        // Verify user has ReadMessageHistory permission for the conversation's channel
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();

        // Find the channel associated with this conversation
        var channel = await context.Channels
            .Where(c => c.ConversationId == conversationId && c.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (channel == null)
        {
            throw new HubException("Conversation not found");
        }

        // Check permission
        var permissionResult = await roleService.EnsureChannelRole(
            userId.Value,
            channel.Id,
            Role.ReadMessageHistory);

        if (permissionResult.IsFailure)
        {
            throw new HubException("Forbidden");
        }

        // Add to conversation group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");
        _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);
    }

    public async Task LeaveConversation(long conversationId)
    {
        if (conversationId <= 0)
        {
            throw new HubException("Invalid id");
        }

        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        _logger.LogDebug("User {UserId} leaving conversation {ConversationId}", userId, conversationId);

        // Remove from conversation group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");
        _logger.LogInformation("User {UserId} left conversation {ConversationId}", userId, conversationId);
    }

    public async Task StartTyping(long conversationId)
    {
        if (conversationId <= 0)
        {
            throw new HubException("Invalid id");
        }

        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        _logger.LogDebug("User {UserId} typing in conversation {ConversationId}", userId, conversationId);

        // Verify user has SendMessages permission for the conversation's channel
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();

        // Find the channel associated with this conversation
        var channel = await context.Channels
            .Where(c => c.ConversationId == conversationId && c.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (channel == null)
        {
            throw new HubException("Conversation not found");
        }

        // Check permission
        var permissionResult = await roleService.EnsureChannelRole(
            userId.Value,
            channel.Id,
            Role.SendMessages);

        if (permissionResult.IsFailure)
        {
            throw new HubException("Forbidden");
        }

        // Rate limit: max 1 typing event per 3 seconds per user per conversation
        var db = _redis.GetDatabase();
        var typingKey = $"{_channelPrefix}:typing:{conversationId}:{userId}";

        // Check if key already exists (rate limiting)
        var exists = await db.KeyExistsAsync(typingKey);
        if (exists)
        {
            // Already broadcasting typing, skip
            return;
        }

        // Set Redis key with 8-second TTL
        await db.StringSetAsync(typingKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), TimeSpan.FromSeconds(8));

        // Broadcast typing event to conversation group
        var payload = new
        {
            userId = userId.Value,
            conversationId,
            timestamp = DateTimeOffset.UtcNow
        };

        await Clients.Group($"conversation:{conversationId}")
            .SendAsync("Chat_TypingStarted", payload);

        _logger.LogDebug("User {UserId} typing broadcast to conversation {ConversationId}", userId, conversationId);
    }

    /// <summary>
    /// Withdraw a typing notice, when someone clears what they were composing.
    /// </summary>
    /// <remarks>
    /// Without this the notice only ever expired: someone who typed a word and
    /// deleted it went on "typing" on every other screen for the full eight
    /// seconds. Deliberately cheaper than <see cref="StartTyping"/> - it does not
    /// re-check SendMessages, because withdrawing a claim about yourself needs no
    /// permission, and someone whose permission was revoked mid-compose must
    /// still be able to stop appearing to type. Clearing the rate-limit key is
    /// what lets the next keystroke broadcast again immediately.
    /// </remarks>
    public async Task StopTyping(long conversationId)
    {
        if (conversationId <= 0)
        {
            throw new HubException("Invalid id");
        }

        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"{_channelPrefix}:typing:{conversationId}:{userId}");

        await Clients.Group($"conversation:{conversationId}")
            .SendAsync("Chat_TypingStopped", new
            {
                userId = userId.Value,
                conversationId,
                timestamp = DateTimeOffset.UtcNow,
            });

        _logger.LogDebug("User {UserId} stopped typing in conversation {ConversationId}", userId, conversationId);
    }
}
