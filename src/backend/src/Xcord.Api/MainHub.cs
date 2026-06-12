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
///
/// Methods are organized by namespace into partial files:
/// - <see cref="MainHub"/> (this file): construction, connection lifecycle, shared helpers
/// - MainHub.Chat.cs: Chat_* methods (conversation join/leave, typing)
/// - MainHub.Voice.cs: Voice_* methods (channel join/leave, voice state, streaming)
/// - MainHub.Presence.cs: Presence_* methods (status, heartbeat)
/// - MainHub.Notify.cs: Notify_* methods (notification subscription/dispatch)
/// </summary>
[Authorize]
public partial class MainHub : Hub
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly string _channelPrefix;
    private readonly string _instanceDomain;
    private readonly ILogger<MainHub> _logger;
    private readonly IPresenceService _presenceService;
    private readonly IPresenceNotifier _presenceNotifier;
    private readonly LiveKitOptions _options;
    private readonly TierOptions _tierOptions;

    public MainHub(
        IServiceScopeFactory serviceScopeFactory,
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions,
        IOptions<InstanceOptions> instanceOptions,
        IOptions<LiveKitOptions> liveKitOptions,
        IOptions<TierOptions> tierOptions,
        ILogger<MainHub> logger,
        IPresenceService presenceService,
        IPresenceNotifier presenceNotifier)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _redis = redis;
        _channelPrefix = redisOptions.Value.ChannelPrefix;
        _instanceDomain = instanceOptions.Value.Domain;
        _options = liveKitOptions.Value;
        _tierOptions = tierOptions.Value;
        _logger = logger;
        _presenceService = presenceService;
        _presenceNotifier = presenceNotifier;
    }

    private const string ServerIdsCacheKey = "ServerIds";
    private static readonly TimeSpan ServerIdsCacheTtl = TimeSpan.FromSeconds(60);

    private sealed record ServerIdsCacheEntry(List<long> Ids, DateTimeOffset FetchedAt);

    /// <summary>
    /// Returns the user's server IDs, cached per connection with a short TTL.
    /// The TTL bounds how long presence fan-out can target a stale server list
    /// after the user joins or leaves a server mid-connection (the old cache
    /// lived for the whole connection and was never invalidated).
    /// </summary>
    private async Task<List<long>> GetServerIdsCachedAsync(AppDbContext context, long userId)
    {
        if (Context.Items.TryGetValue(ServerIdsCacheKey, out var cached)
            && cached is ServerIdsCacheEntry entry
            && DateTimeOffset.UtcNow - entry.FetchedAt < ServerIdsCacheTtl)
        {
            return entry.Ids;
        }

        var serverIds = await context.ServerMembers
            .Where(sm => sm.UserId == userId)
            .Select(sm => sm.ServerId)
            .ToListAsync();

        Context.Items[ServerIdsCacheKey] = new ServerIdsCacheEntry(serverIds, DateTimeOffset.UtcNow);
        return serverIds;
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

        var serverIds = await GetServerIdsCachedAsync(context, userId.Value);

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

        // Get server IDs from the connection cache (refetched when stale)
        var serverIds = await GetServerIdsCachedAsync(context, userId.Value);

        // Remove from server groups
        foreach (var sid in serverIds)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"server:{sid}");
        }

        // Remove presence using presence service
        await _presenceService.RemovePresenceAsync(userId.Value, serverIds);

        // Notify servers of offline status
        await _presenceNotifier.NotifyPresenceChangedAsync(userId.Value, PresenceStatus.Offline, serverIds);

        // Clean up any VoiceState entities and notify (skip soft-deleted channels)
        var voiceStates = await context.VoiceStates
            .Where(vs => vs.UserId == userId.Value)
            .Join(context.Channels.Where(c => c.DeletedAt == null),
                vs => vs.ChannelId,
                c => c.Id,
                (vs, c) => new { VoiceState = vs, Channel = c })
            .ToListAsync();

        foreach (var vs in voiceStates)
        {
            var voiceState = vs.VoiceState;
            var channelId = voiceState.ChannelId;
            var wasStreaming = voiceState.IsStreaming;
            var serverId = vs.Channel.ServerId;

            context.VoiceStates.Remove(voiceState);

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"voice:{serverId}:{channelId}");

            await Clients.Group($"voice:{serverId}:{channelId}")
                .SendAsync("Voice_StateUpdated", new
                {
                    userId = userId.Value,
                    channelId,
                    isConnected = false
                });

            if (wasStreaming)
            {
                await Clients.Group($"voice:{serverId}:{channelId}")
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

    // -------------------------------------------------------------------------
    // Shared helpers used by every partial
    // -------------------------------------------------------------------------

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
