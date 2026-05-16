using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Api;

/// <summary>
/// Internal helpers shared between Voice_* hub methods (see MainHub.Voice.cs).
/// Kept in a separate partial file so the public hub method surface stays focused.
/// </summary>
public partial class MainHub
{
    private VideoQualityConstraints BuildVoiceQualityConstraints() => new()
    {
        MaxAudioBitrateKbps = _tierOptions.MaxAudioBitrateKbps,
        MaxVideoBitrateKbps = _tierOptions.MaxVideoBitrateKbps,
        MaxVideoWidth = _tierOptions.MaxVideoWidth,
        MaxVideoHeight = _tierOptions.MaxVideoHeight,
        MaxVideoFps = _tierOptions.MaxVideoFps,
        MaxScreenShareBitrateKbps = _tierOptions.MaxScreenShareBitrateKbps,
        EnableSimulcast = _tierOptions.CanUseSimulcast
    };

    private object BuildVoiceQualityConfigPayload() => new
    {
        maxAudioBitrateKbps = _tierOptions.MaxAudioBitrateKbps,
        maxVideoBitrateKbps = _tierOptions.MaxVideoBitrateKbps,
        maxVideoWidth = _tierOptions.MaxVideoWidth,
        maxVideoHeight = _tierOptions.MaxVideoHeight,
        maxVideoFps = _tierOptions.MaxVideoFps,
        maxScreenShareBitrateKbps = _tierOptions.MaxScreenShareBitrateKbps,
        enableSimulcast = _tierOptions.CanUseSimulcast
    };

    private object BuildVoiceTokenResponse(string token, string roomName) => new
    {
        token,
        roomName,
        livekitUrl = _options.Host,
        qualityConfig = BuildVoiceQualityConfigPayload()
    };

    private static Task<long> GetServerIdForChannelAsync(AppDbContext context, long channelId) =>
        context.Channels
            .Where(c => c.Id == channelId)
            .Select(c => c.ServerId)
            .FirstOrDefaultAsync();

    private async Task BroadcastVoiceDisconnectedAsync(long serverId, long channelId, long userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"voice:{serverId}:{channelId}");
        await Clients.Group($"voice:{serverId}:{channelId}")
            .SendAsync("Voice_StateUpdated", new
            {
                userId,
                channelId,
                isConnected = false
            });
    }
}
