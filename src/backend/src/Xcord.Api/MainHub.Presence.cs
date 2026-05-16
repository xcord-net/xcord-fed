using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Api;

/// <summary>
/// Presence_* methods: status updates and heartbeat.
/// </summary>
public partial class MainHub
{
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
}
