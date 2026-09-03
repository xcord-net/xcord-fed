using System.Collections.Concurrent;
using Xcord.Infrastructure.Services;

namespace Xcord.Tests.Integration.Fixtures;

/// <summary>
/// Test double for <see cref="ILiveKitService"/> that never contacts a real LiveKit
/// server. Generates deterministic egress IDs and JWT-shaped tokens, and records
/// calls so tests can assert interactions if needed.
/// </summary>
public sealed class FakeLiveKitService : ILiveKitService
{
    private int _egressCounter;

    public ConcurrentBag<string> StartedEgresses { get; } = new();
    public ConcurrentBag<string> StoppedEgresses { get; } = new();

    public string GenerateToken(
        long userId,
        string roomName,
        bool canPublish,
        bool canSubscribe,
        bool canPublishData,
        bool canScreenShare,
        TimeSpan ttl,
        VideoQualityConstraints? qualityConstraints = null)
    {
        // Return a placeholder that looks like a JWT (three dot-separated base64 parts)
        // so any downstream string-not-empty assertions pass.
        return $"fake.token.{userId}.{roomName}";
    }

    public Task RemoveParticipantAsync(string roomName, string participantIdentity)
        => Task.CompletedTask;

    /// <summary>
    /// Every control message published to a room, so a test can assert that a
    /// stage or layout change reached the compositor without restarting egress.
    /// </summary>
    public ConcurrentBag<(string Room, string Topic, string Payload)> SentData { get; } = new();

    public Task SendDataAsync(string roomName, string topic, string payload, CancellationToken ct)
    {
        SentData.Add((roomName, topic, payload));
        return Task.CompletedTask;
    }

    public Task<string> StartRoomCompositeEgressAsync(
        string roomName,
        string templateUrl,
        IEnumerable<EgressOutput> outputs,
        CancellationToken ct)
    {
        var id = $"EG_test_{Interlocked.Increment(ref _egressCounter)}";
        StartedEgresses.Add(id);
        return Task.FromResult(id);
    }

    public Task StopEgressAsync(string egressId, CancellationToken ct)
    {
        StoppedEgresses.Add(egressId);
        return Task.CompletedTask;
    }

    public Task<string> RestartEgressWithNewOutputsAsync(
        string oldEgressId,
        string roomName,
        string templateUrl,
        IEnumerable<EgressOutput> outputs,
        CancellationToken ct)
    {
        StoppedEgresses.Add(oldEgressId);
        var id = $"EG_test_{Interlocked.Increment(ref _egressCounter)}";
        StartedEgresses.Add(id);
        return Task.FromResult(id);
    }
}
