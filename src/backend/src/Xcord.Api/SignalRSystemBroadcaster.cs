using Microsoft.AspNetCore.SignalR;
using Xcord.Infrastructure.Services;

namespace Xcord.Api;

/// <summary>
/// ISystemBroadcaster implementation that broadcasts to all connected SignalR clients
/// via IHubContext&lt;MainHub&gt;.
/// </summary>
public sealed class SignalRSystemBroadcaster : ISystemBroadcaster
{
    private readonly IHubContext<MainHub> _hubContext;

    public SignalRSystemBroadcaster(IHubContext<MainHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastAsync(string eventName, object payload, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(eventName, payload, cancellationToken);
    }
}
