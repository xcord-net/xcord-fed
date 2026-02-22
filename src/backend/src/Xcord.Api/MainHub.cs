using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Infrastructure.Services;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;

namespace Xcord.Api;

/// <summary>
/// Single unified SignalR hub for all real-time functionality.
/// Client reconnect schedule: [0, 1s, 2s, 5s, 10s, 30s]
/// </summary>
[Authorize]
public class MainHub : Hub
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly string _channelPrefix;
    private readonly string _instanceDomain;
    private readonly ILogger<MainHub> _logger;
    private readonly IPresenceService _presenceService;
    private readonly IPresenceNotifier _presenceNotifier;
    private readonly LiveKitOptions _options;

    public MainHub(
        IServiceScopeFactory serviceScopeFactory,
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions,
        IOptions<InstanceOptions> instanceOptions,
        IOptions<LiveKitOptions> liveKitOptions,
        ILogger<MainHub> logger,
        IPresenceService presenceService,
        IPresenceNotifier presenceNotifier)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _redis = redis;
        _channelPrefix = redisOptions.Value.ChannelPrefix;
        _instanceDomain = instanceOptions.Value.Domain;
        _options = liveKitOptions.Value;
        _logger = logger;
        _presenceService = presenceService;
        _presenceNotifier = presenceNotifier;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            _logger.LogWarning("Connection attempt without valid userId");
            Context.Abort();
            return;
        }

        _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);

        // Add to user group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        // Get user's server IDs
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var serverIds = await context.ServerMembers
            .Where(sm => sm.UserId == userId.Value)
            .Select(sm => sm.ServerId)
            .ToListAsync();

        // Store server IDs in connection items for later use
        Context.Items["ServerIds"] = serverIds;

        // Add to server groups so this connection receives server-wide broadcasts (e.g. Presence_Updated)
        foreach (var sid in serverIds)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"server:{sid}");
        }

        // Set presence to Online using presence service
        await _presenceService.SetStatusAsync(userId.Value, PresenceStatus.Online);

        // Add user to presence sorted sets and update scores
        await _presenceService.HeartbeatAsync(userId.Value, serverIds);

        // Store connectionId in Redis for tracking
        var db = _redis.GetDatabase();
        var presenceKey = $"{_channelPrefix}:presence:user:{userId}";
        await db.HashSetAsync(presenceKey, "connectionId", Context.ConnectionId);

        // Notify servers of online status
        await _presenceNotifier.NotifyPresenceChangedAsync(userId.Value, PresenceStatus.Online, serverIds);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await base.OnDisconnectedAsync(exception);
            return;
        }

        _logger.LogInformation("User {UserId} disconnected from connection {ConnectionId}", userId, Context.ConnectionId);

        // Remove from user group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");

        // Create scope for DB operations
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get server IDs from connection items or DB
        List<long> serverIds;
        if (Context.Items.TryGetValue("ServerIds", out var cachedServerIds) && cachedServerIds is List<long> ids)
        {
            serverIds = ids;
        }
        else
        {
            serverIds = await context.ServerMembers
                .Where(sm => sm.UserId == userId.Value)
                .Select(sm => sm.ServerId)
                .ToListAsync();
        }

        // Remove from server groups
        foreach (var sid in serverIds)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"server:{sid}");
        }

        // Remove presence using presence service
        await _presenceService.RemovePresenceAsync(userId.Value, serverIds);

        // Notify servers of offline status
        await _presenceNotifier.NotifyPresenceChangedAsync(userId.Value, PresenceStatus.Offline, serverIds);

        // Clean up any VoiceState entities and notify
        var voiceStates = await context.VoiceStates
            .Where(vs => vs.UserId == userId.Value)
            .ToListAsync();

        foreach (var voiceState in voiceStates)
        {
            var channelId = voiceState.ChannelId;
            var wasStreaming = voiceState.IsStreaming;

            // Remove VoiceState entity
            context.VoiceStates.Remove(voiceState);

            // Remove from SignalR voice group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"voice:{channelId}");

            // Broadcast state update to voice channel
            await Clients.Group($"voice:{channelId}")
                .SendAsync("Voice_StateUpdated", new
                {
                    userId = userId.Value,
                    channelId,
                    isConnected = false
                });

            // If user was streaming, broadcast stream ended
            if (wasStreaming)
            {
                await Clients.Group($"voice:{channelId}")
                    .SendAsync("Voice_StreamEnded", new
                    {
                        userId = userId.Value,
                        channelId
                    });
            }

            _logger.LogInformation("User {UserId} removed from voice channel {ChannelId} on disconnect", userId, channelId);
        }

        if (voiceStates.Any())
        {
            await context.SaveChangesAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(long conversationId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        _logger.LogDebug("User {UserId} attempting to join conversation {ConversationId}", userId, conversationId);

        // Verify user has ReadMessageHistory permission for the conversation's channel
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();

        // Find the channel associated with this conversation
        var channel = await context.Channels
            .Where(c => c.ConversationId == conversationId && c.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (channel == null)
        {
            throw new HubException("Conversation not found");
        }

        // Check permission
        var permissionResult = await permissionService.EnsureChannelPermission(
            userId.Value,
            channel.Id,
            Permission.ReadMessageHistory);

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
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        _logger.LogDebug("User {UserId} typing in conversation {ConversationId}", userId, conversationId);

        // Verify user has SendMessages permission for the conversation's channel
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();

        // Find the channel associated with this conversation
        var channel = await context.Channels
            .Where(c => c.ConversationId == conversationId && c.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (channel == null)
        {
            throw new HubException("Conversation not found");
        }

        // Check permission
        var permissionResult = await permissionService.EnsureChannelPermission(
            userId.Value,
            channel.Id,
            Permission.SendMessages);

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

    public async Task UpdateStatus(string status)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        // Parse status to PresenceStatus enum
        if (!Enum.TryParse<PresenceStatus>(status, ignoreCase: true, out var presenceStatus))
        {
            throw new HubException("Invalid status. Valid values: Online, Away, DND, Offline");
        }

        _logger.LogDebug("User {UserId} updating status to {Status}", userId, presenceStatus);

        // Update status in presence service
        await _presenceService.SetStatusAsync(userId.Value, presenceStatus);

        // Get user's server IDs from connection items or DB
        List<long> serverIds;
        if (Context.Items.TryGetValue("ServerIds", out var cachedServerIds) && cachedServerIds is List<long> ids)
        {
            serverIds = ids;
        }
        else
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            serverIds = await context.ServerMembers
                .Where(sm => sm.UserId == userId.Value)
                .Select(sm => sm.ServerId)
                .ToListAsync();

            // Cache for future use
            Context.Items["ServerIds"] = serverIds;
        }

        // If status is Online, Away, or DND, update heartbeat
        if (presenceStatus != PresenceStatus.Offline)
        {
            await _presenceService.HeartbeatAsync(userId.Value, serverIds);
        }

        // Notify all servers of the status change
        await _presenceNotifier.NotifyPresenceChangedAsync(userId.Value, presenceStatus, serverIds);

        _logger.LogInformation("User {UserId} status updated to {Status}", userId, presenceStatus);
    }

    public async Task Heartbeat()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        // Get user's server IDs from connection items or DB
        List<long> serverIds;
        if (Context.Items.TryGetValue("ServerIds", out var cachedServerIds) && cachedServerIds is List<long> ids)
        {
            serverIds = ids;
        }
        else
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            serverIds = await context.ServerMembers
                .Where(sm => sm.UserId == userId.Value)
                .Select(sm => sm.ServerId)
                .ToListAsync();

            // Cache for future use
            Context.Items["ServerIds"] = serverIds;
        }

        // Update heartbeat (silent operation, no broadcast)
        await _presenceService.HeartbeatAsync(userId.Value, serverIds);
    }

    public async Task<object> JoinVoiceChannel(long channelId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        _logger.LogInformation("User {UserId} attempting to join voice channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
        var liveKitService = scope.ServiceProvider.GetRequiredService<ILiveKitService>();

        // Check Connect permission
        var permissionResult = await permissionService.EnsureChannelPermission(
            userId.Value,
            channelId,
            Permission.Connect);

        if (permissionResult.IsFailure)
        {
            throw new HubException("Forbidden");
        }

        // If user is already in a voice channel, leave it first
        var existingState = await context.VoiceStates
            .FirstOrDefaultAsync(vs => vs.UserId == userId.Value);

        if (existingState != null)
        {
            var oldChannelId = existingState.ChannelId;
            context.VoiceStates.Remove(existingState);
            await context.SaveChangesAsync();

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"voice:{oldChannelId}");

            await Clients.Group($"voice:{oldChannelId}")
                .SendAsync("Voice_StateUpdated", new
                {
                    userId = userId.Value,
                    channelId = oldChannelId,
                    isConnected = false
                });

            _logger.LogInformation("User {UserId} left voice channel {ChannelId}", userId, oldChannelId);
        }

        // Check if user has ShareScreen permission
        var channelPerms = await permissionService.GetChannelPermissions(userId.Value, channelId);
        var canScreenShare = (channelPerms & (long)Permission.ShareScreen) != 0;

        // Generate LiveKit room name
        var roomName = $"{_instanceDomain}:voice:{channelId}";

        // Generate LiveKit token (30 min TTL)
        var token = liveKitService.GenerateToken(
            userId: userId.Value,
            roomName: roomName,
            canPublish: true,
            canSubscribe: true,
            canPublishData: true,
            canScreenShare: canScreenShare,
            ttl: TimeSpan.FromMinutes(30));

        // Create VoiceState entity
        var voiceState = new VoiceState
        {
            UserId = userId.Value,
            ChannelId = channelId,
            IsMuted = false,
            IsDeafened = false,
            IsStreaming = false,
            IsServerMuted = false,
            IsServerDeafened = false,
            JoinedAt = DateTime.UtcNow
        };

        context.VoiceStates.Add(voiceState);
        await context.SaveChangesAsync();

        // Add to SignalR voice group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"voice:{channelId}");

        // Broadcast state update to voice channel
        await Clients.Group($"voice:{channelId}")
            .SendAsync("Voice_StateUpdated", new
            {
                userId = userId.Value,
                channelId,
                isConnected = true,
                isMuted = false,
                isDeafened = false,
                isStreaming = false,
                isServerMuted = false,
                isServerDeafened = false,
                joinedAt = voiceState.JoinedAt
            });

        _logger.LogInformation("User {UserId} joined voice channel {ChannelId}", userId, channelId);

        return new
        {
            token,
            roomName,
            livekitUrl = _options.Host
        };
    }

    public async Task LeaveVoiceChannel(long channelId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        _logger.LogInformation("User {UserId} leaving voice channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find and remove VoiceState
        var voiceState = await context.VoiceStates
            .FirstOrDefaultAsync(vs => vs.UserId == userId.Value && vs.ChannelId == channelId);

        if (voiceState == null)
        {
            _logger.LogWarning("User {UserId} tried to leave voice channel {ChannelId} but was not in it", userId, channelId);
            return;
        }

        context.VoiceStates.Remove(voiceState);
        await context.SaveChangesAsync();

        // Remove from SignalR voice group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"voice:{channelId}");

        // Broadcast state update
        await Clients.Group($"voice:{channelId}")
            .SendAsync("Voice_StateUpdated", new
            {
                userId = userId.Value,
                channelId,
                isConnected = false
            });

        _logger.LogInformation("User {UserId} left voice channel {ChannelId}", userId, channelId);
    }

    public async Task UpdateVoiceState(long channelId, bool? isMuted, bool? isDeafened, bool? isStreaming)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        _logger.LogDebug("User {UserId} updating voice state in channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();

        // Find VoiceState
        var voiceState = await context.VoiceStates
            .FirstOrDefaultAsync(vs => vs.UserId == userId.Value && vs.ChannelId == channelId);

        if (voiceState == null)
        {
            throw new HubException("Not in voice channel");
        }

        // If isStreaming is being changed to true, check ShareScreen permission
        if (isStreaming == true && !voiceState.IsStreaming)
        {
            var permissionResult = await permissionService.EnsureChannelPermission(
                userId.Value,
                channelId,
                Permission.ShareScreen);

            if (permissionResult.IsFailure)
            {
                throw new HubException("Forbidden: Missing ShareScreen permission");
            }
        }

        // Update state
        if (isMuted.HasValue)
        {
            voiceState.IsMuted = isMuted.Value;
        }

        if (isDeafened.HasValue)
        {
            voiceState.IsDeafened = isDeafened.Value;
        }

        if (isStreaming.HasValue)
        {
            voiceState.IsStreaming = isStreaming.Value;
        }

        await context.SaveChangesAsync();

        // Broadcast state update
        await Clients.Group($"voice:{channelId}")
            .SendAsync("Voice_StateUpdated", new
            {
                userId = userId.Value,
                channelId,
                isConnected = true,
                isMuted = voiceState.IsMuted,
                isDeafened = voiceState.IsDeafened,
                isStreaming = voiceState.IsStreaming,
                isServerMuted = voiceState.IsServerMuted,
                isServerDeafened = voiceState.IsServerDeafened,
                joinedAt = voiceState.JoinedAt
            });

        _logger.LogInformation("User {UserId} updated voice state in channel {ChannelId}", userId, channelId);
    }

    public async Task<object> RefreshVoiceToken(long channelId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        _logger.LogDebug("User {UserId} refreshing voice token for channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
        var liveKitService = scope.ServiceProvider.GetRequiredService<ILiveKitService>();

        // Verify user has VoiceState (still connected)
        var voiceState = await context.VoiceStates
            .FirstOrDefaultAsync(vs => vs.UserId == userId.Value && vs.ChannelId == channelId);

        if (voiceState == null)
        {
            throw new HubException("Not in voice channel");
        }

        // Check if user still has ShareScreen permission
        var channelPerms = await permissionService.GetChannelPermissions(userId.Value, channelId);
        var canScreenShare = (channelPerms & (long)Permission.ShareScreen) != 0;

        // Generate LiveKit room name
        var roomName = $"{_instanceDomain}:voice:{channelId}";

        // Generate new LiveKit token
        var token = liveKitService.GenerateToken(
            userId: userId.Value,
            roomName: roomName,
            canPublish: true,
            canSubscribe: true,
            canPublishData: true,
            canScreenShare: canScreenShare,
            ttl: TimeSpan.FromMinutes(30));

        _logger.LogInformation("User {UserId} refreshed voice token for channel {ChannelId}", userId, channelId);

        return new
        {
            token,
            roomName,
            livekitUrl = _options.Host
        };
    }

    public async Task StartStream(long channelId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        _logger.LogInformation("User {UserId} starting stream in channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();

        // Verify user has VoiceState in channel (must be in voice)
        var voiceState = await context.VoiceStates
            .FirstOrDefaultAsync(vs => vs.UserId == userId.Value && vs.ChannelId == channelId);

        if (voiceState == null)
        {
            throw new HubException("Not in voice channel");
        }

        // Verify ShareScreen permission
        var permissionResult = await permissionService.EnsureChannelPermission(
            userId.Value,
            channelId,
            Permission.ShareScreen);

        if (permissionResult.IsFailure)
        {
            throw new HubException("Forbidden: Missing ShareScreen permission");
        }

        // Set IsStreaming to true
        voiceState.IsStreaming = true;
        await context.SaveChangesAsync();

        // Broadcast stream started to voice channel
        await Clients.Group($"voice:{channelId}")
            .SendAsync("Voice_StreamStarted", new
            {
                userId = userId.Value,
                channelId
            });

        _logger.LogInformation("User {UserId} started stream in channel {ChannelId}", userId, channelId);
    }

    public async Task StopStream(long channelId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("Unauthorized");
        }

        _logger.LogInformation("User {UserId} stopping stream in channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Verify user has VoiceState in channel
        var voiceState = await context.VoiceStates
            .FirstOrDefaultAsync(vs => vs.UserId == userId.Value && vs.ChannelId == channelId);

        if (voiceState == null)
        {
            throw new HubException("Not in voice channel");
        }

        // Set IsStreaming to false
        voiceState.IsStreaming = false;
        await context.SaveChangesAsync();

        // Broadcast stream ended to voice channel
        await Clients.Group($"voice:{channelId}")
            .SendAsync("Voice_StreamEnded", new
            {
                userId = userId.Value,
                channelId
            });

        _logger.LogInformation("User {UserId} stopped stream in channel {ChannelId}", userId, channelId);
    }

    private long? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !long.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }
}
