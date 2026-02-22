using Microsoft.AspNetCore.SignalR;
using Xcord.Api;
using Xcord.Entities;
using Xcord.Infrastructure.Services;

namespace Xcord.Api;

/// <summary>
/// SignalR implementation of presence notifier.
/// Broadcasts presence changes to server groups via MainHub.
/// </summary>
public sealed class SignalRPresenceNotifier : IPresenceNotifier
{
    private readonly IHubContext<MainHub> _hubContext;

    public SignalRPresenceNotifier(IHubContext<MainHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyPresenceChangedAsync(long userId, PresenceStatus status, IEnumerable<long> serverIds)
    {
        var payload = new
        {
            userId,
            status = status.ToString(),
            lastSeen = DateTimeOffset.UtcNow
        };

        var tasks = serverIds.Select(serverId =>
            _hubContext.Clients.Group($"server:{serverId}")
                .SendAsync("Presence_Updated", payload)
        );

        await Task.WhenAll(tasks);
    }
}
