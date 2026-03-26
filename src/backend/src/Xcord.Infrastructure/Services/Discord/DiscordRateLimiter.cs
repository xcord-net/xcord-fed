namespace Xcord.Infrastructure.Services.Discord;

/// <summary>
/// Token bucket rate limiter for Discord REST API calls.
/// Enforces 4 requests per 5 seconds (safety margin under Discord's 5 req/5s limit).
/// </summary>
public sealed class DiscordRateLimiter
{
    private const int MaxRequests = 4;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Queue<DateTimeOffset> _timestamps = new();

    /// <summary>
    /// Waits until a rate limit slot is available, then claims it.
    /// </summary>
    public async Task WaitAsync(CancellationToken ct = default)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var now = DateTimeOffset.UtcNow;
                var cutoff = now - Window;

                // Evict timestamps outside the current window
                while (_timestamps.Count > 0 && _timestamps.Peek() <= cutoff)
                    _timestamps.Dequeue();

                if (_timestamps.Count < MaxRequests)
                {
                    _timestamps.Enqueue(now);
                    return;
                }

                // Calculate how long until the oldest slot expires
                var oldest = _timestamps.Peek();
                var delay = oldest - cutoff;
                _lock.Release();

                await Task.Delay(delay, ct).ConfigureAwait(false);
                // Loop back to try again
                continue;
            }
            catch
            {
                // Only release if we still hold the lock (i.e., we didn't return or release early)
                if (_lock.CurrentCount == 0)
                    _lock.Release();
                throw;
            }
        }
    }
}
