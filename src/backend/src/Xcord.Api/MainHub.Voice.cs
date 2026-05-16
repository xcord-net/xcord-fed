using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Role = Xcord.Entities.Role;
using Xcord.Infrastructure.Services;
using Xcord.Infrastructure.Data;

namespace Xcord.Api;

/// <summary>
/// Voice_* methods: voice channel join/leave, voice state, token refresh, streaming.
/// Helpers shared across these methods live in MainHub.VoiceHelpers.cs.
/// </summary>
public partial class MainHub
{
    public async Task<object> JoinVoiceChannel(long channelId)
    {
        if (channelId <= 0) throw new HubException("Invalid id");

        var userId = GetUserId() ?? throw new HubException("Unauthorized");

        if (!_tierOptions.CanUseVoiceChannels)
        {
            throw new HubException("Voice channels are not available on your current plan");
        }

        _logger.LogInformation("User {UserId} attempting to join voice channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
        var liveKitService = scope.ServiceProvider.GetRequiredService<ILiveKitService>();

        var channel = await context.Channels
            .FirstOrDefaultAsync(c => c.Id == channelId && c.DeletedAt == null)
            ?? throw new HubException("Channel not found");

        var serverId = channel.ServerId;

        var permissionResult = await roleService.EnsureChannelRole(userId, channelId, Role.Connect);
        if (permissionResult.IsFailure) throw new HubException("Forbidden");

        // Concurrency check: count instance-wide voice participants (excluding this user)
        if (_tierOptions.MaxVoiceConcurrency > 0)
        {
            var currentVoiceCount = await context.VoiceStates.CountAsync(vs => vs.UserId != userId);
            if (currentVoiceCount >= _tierOptions.MaxVoiceConcurrency)
            {
                throw new HubException("Voice participant limit reached - upgrade your plan for more concurrent participants");
            }
        }

        // If user is already in a voice channel, leave it first
        var existingState = await context.VoiceStates.FirstOrDefaultAsync(vs => vs.UserId == userId);
        if (existingState != null)
        {
            var oldChannelId = existingState.ChannelId;
            var oldServerId = await GetServerIdForChannelAsync(context, oldChannelId);
            context.VoiceStates.Remove(existingState);
            await context.SaveChangesAsync();

            await BroadcastVoiceDisconnectedAsync(oldServerId, oldChannelId, userId);
            _logger.LogInformation("User {UserId} left voice channel {ChannelId}", userId, oldChannelId);
        }

        var channelPerms = await roleService.GetChannelRoles(userId, channelId);
        var canScreenShare = (channelPerms & (long)Role.ShareScreen) != 0;

        var roomName = $"{_instanceDomain}:voice:{channelId}";
        var qualityConstraints = BuildVoiceQualityConstraints();

        // Audio publish is always allowed in voice channels; video publish requires Video tier.
        var token = liveKitService.GenerateToken(
            userId: userId,
            roomName: roomName,
            canPublish: true,
            canSubscribe: true,
            canPublishData: true,
            canScreenShare: canScreenShare && _tierOptions.CanUseVideoChannels,
            ttl: TimeSpan.FromMinutes(30),
            qualityConstraints: qualityConstraints);

        var voiceState = new VoiceState
        {
            UserId = userId,
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

        await Groups.AddToGroupAsync(Context.ConnectionId, $"voice:{serverId}:{channelId}");

        await Clients.Group($"voice:{serverId}:{channelId}")
            .SendAsync("Voice_StateUpdated", new
            {
                userId,
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

        return BuildVoiceTokenResponse(token, roomName);
    }

    public async Task LeaveVoiceChannel(long channelId)
    {
        if (channelId <= 0) throw new HubException("Invalid id");

        var userId = GetUserId() ?? throw new HubException("Unauthorized");

        _logger.LogInformation("User {UserId} leaving voice channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var voiceState = await context.VoiceStates
            .FirstOrDefaultAsync(vs => vs.UserId == userId && vs.ChannelId == channelId);

        if (voiceState == null)
        {
            _logger.LogWarning("User {UserId} tried to leave voice channel {ChannelId} but was not in it", userId, channelId);
            return;
        }

        var serverId = await GetServerIdForChannelAsync(context, channelId);

        context.VoiceStates.Remove(voiceState);
        await context.SaveChangesAsync();

        await BroadcastVoiceDisconnectedAsync(serverId, channelId, userId);

        _logger.LogInformation("User {UserId} left voice channel {ChannelId}", userId, channelId);
    }

    public async Task UpdateVoiceState(long channelId, bool? isMuted, bool? isDeafened, bool? isStreaming)
    {
        if (channelId <= 0) throw new HubException("Invalid id");

        var userId = GetUserId() ?? throw new HubException("Unauthorized");

        _logger.LogDebug("User {UserId} updating voice state in channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();

        var voiceState = await context.VoiceStates
            .FirstOrDefaultAsync(vs => vs.UserId == userId && vs.ChannelId == channelId)
            ?? throw new HubException("Not in voice channel");

        var serverId = await GetServerIdForChannelAsync(context, channelId);

        // If isStreaming is being changed to true, check ShareScreen permission
        if (isStreaming == true && !voiceState.IsStreaming)
        {
            var permissionResult = await roleService.EnsureChannelRole(userId, channelId, Role.ShareScreen);
            if (permissionResult.IsFailure)
            {
                throw new HubException("Forbidden: Missing ShareScreen permission");
            }
        }

        if (isMuted.HasValue) voiceState.IsMuted = isMuted.Value;
        if (isDeafened.HasValue) voiceState.IsDeafened = isDeafened.Value;
        if (isStreaming.HasValue) voiceState.IsStreaming = isStreaming.Value;

        await context.SaveChangesAsync();

        await Clients.Group($"voice:{serverId}:{channelId}")
            .SendAsync("Voice_StateUpdated", new
            {
                userId,
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
        if (channelId <= 0) throw new HubException("Invalid id");

        var userId = GetUserId() ?? throw new HubException("Unauthorized");

        _logger.LogDebug("User {UserId} refreshing voice token for channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
        var liveKitService = scope.ServiceProvider.GetRequiredService<ILiveKitService>();

        var channel = await context.Channels
            .FirstOrDefaultAsync(c => c.Id == channelId && c.DeletedAt == null)
            ?? throw new HubException("Channel not found");

        var serverId = channel.ServerId;

        var connectResult = await roleService.EnsureChannelRole(userId, channelId, Role.Connect);
        if (connectResult.IsFailure)
        {
            var staleState = await context.VoiceStates
                .FirstOrDefaultAsync(vs => vs.UserId == userId && vs.ChannelId == channelId);

            if (staleState != null)
            {
                context.VoiceStates.Remove(staleState);
                await context.SaveChangesAsync();

                await BroadcastVoiceDisconnectedAsync(serverId, channelId, userId);

                _logger.LogInformation(
                    "User {UserId} removed from voice channel {ChannelId} on Connect revocation during token refresh",
                    userId, channelId);
            }

            throw new HubException("Forbidden");
        }

        var voiceState = await context.VoiceStates
            .FirstOrDefaultAsync(vs => vs.UserId == userId && vs.ChannelId == channelId)
            ?? throw new HubException("Not in voice channel");

        var channelPerms = await roleService.GetChannelRoles(userId, channelId);
        var canScreenShare = (channelPerms & (long)Role.ShareScreen) != 0;

        var roomName = $"{_instanceDomain}:voice:{channelId}";
        var qualityConstraints = BuildVoiceQualityConstraints();

        var token = liveKitService.GenerateToken(
            userId: userId,
            roomName: roomName,
            canPublish: true,
            canSubscribe: true,
            canPublishData: true,
            canScreenShare: canScreenShare,
            ttl: TimeSpan.FromMinutes(30),
            qualityConstraints: qualityConstraints);

        _logger.LogInformation("User {UserId} refreshed voice token for channel {ChannelId}", userId, channelId);

        return BuildVoiceTokenResponse(token, roomName);
    }

    public async Task StartStream(long channelId)
    {
        if (channelId <= 0) throw new HubException("Invalid id");

        var userId = GetUserId() ?? throw new HubException("Unauthorized");

        _logger.LogInformation("User {UserId} starting stream in channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();

        var voiceState = await context.VoiceStates
            .FirstOrDefaultAsync(vs => vs.UserId == userId && vs.ChannelId == channelId)
            ?? throw new HubException("Not in voice channel");

        var serverId = await GetServerIdForChannelAsync(context, channelId);

        var permissionResult = await roleService.EnsureChannelRole(userId, channelId, Role.ShareScreen);
        if (permissionResult.IsFailure)
        {
            throw new HubException("Forbidden: Missing ShareScreen permission");
        }

        // Video concurrency check: count instance-wide active streams
        if (_tierOptions.MaxVideoConcurrency > 0)
        {
            var activeStreamCount = await context.VoiceStates.CountAsync(vs => vs.IsStreaming);
            if (activeStreamCount >= _tierOptions.MaxVideoConcurrency)
            {
                throw new HubException("Video stream limit reached - upgrade your plan for more concurrent streams");
            }
        }

        voiceState.IsStreaming = true;
        await context.SaveChangesAsync();

        await Clients.Group($"voice:{serverId}:{channelId}")
            .SendAsync("Voice_StreamStarted", new { userId, channelId });

        _logger.LogInformation("User {UserId} started stream in channel {ChannelId}", userId, channelId);
    }

    public async Task StopStream(long channelId)
    {
        if (channelId <= 0) throw new HubException("Invalid id");

        var userId = GetUserId() ?? throw new HubException("Unauthorized");

        _logger.LogInformation("User {UserId} stopping stream in channel {ChannelId}", userId, channelId);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var voiceState = await context.VoiceStates
            .FirstOrDefaultAsync(vs => vs.UserId == userId && vs.ChannelId == channelId)
            ?? throw new HubException("Not in voice channel");

        var serverId = await GetServerIdForChannelAsync(context, channelId);

        voiceState.IsStreaming = false;
        await context.SaveChangesAsync();

        await Clients.Group($"voice:{serverId}:{channelId}")
            .SendAsync("Voice_StreamEnded", new { userId, channelId });

        _logger.LogInformation("User {UserId} stopped stream in channel {ChannelId}", userId, channelId);
    }
}
