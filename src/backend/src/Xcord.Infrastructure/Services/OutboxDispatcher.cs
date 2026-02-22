using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that processes unprocessed outbox events and dispatches them via SignalR.
/// Uses pg_notify for low-latency wake-up on INSERT, with polling as a safety-net fallback.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    /// <summary>
    /// The PostgreSQL LISTEN channel name. Must match the trigger's pg_notify channel.
    /// </summary>
    public const string NotifyChannel = "outbox_event_inserted";

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly OutboxOptions _options;
    private readonly IEventDispatcher? _eventDispatcher;

    public OutboxDispatcher(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<OutboxDispatcher> logger,
        IOptions<OutboxOptions> options,
        IEventDispatcher? eventDispatcher = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
        _eventDispatcher = eventDispatcher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OutboxDispatcher starting. pg_notify channel: '{Channel}', fallback polling interval: {FallbackMs}ms",
            NotifyChannel, _options.FallbackPollingIntervalMs);

        // Run the pg_notify listener and the fallback poller concurrently.
        // Either task completing (or throwing) propagates cancellation through the CancellationToken.
        var listenerTask = RunListenerAsync(stoppingToken);
        var fallbackTask = RunFallbackPollerAsync(stoppingToken);

        try
        {
            await Task.WhenAll(listenerTask, fallbackTask);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }

        _logger.LogInformation("OutboxDispatcher stopped");
    }

    // -------------------------------------------------------------------------
    // pg_notify listener — wakes immediately when a new outbox row is inserted
    // -------------------------------------------------------------------------

    private async Task RunListenerAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ListenAndProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "pg_notify listener encountered an error; will reconnect in 5s");

                // Brief delay before reconnecting so we don't spin on a broken DB
                await Task.Delay(5_000, stoppingToken);
            }
        }
    }

    private async Task ListenAndProcessAsync(CancellationToken stoppingToken)
    {
        // Open a dedicated, long-lived Npgsql connection for LISTEN.
        // We obtain the connection string through a scoped DbContext so we don't
        // need to duplicate the configuration.
        string connectionString;
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            connectionString = context.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Database connection string is not configured.");
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(stoppingToken);

        conn.Notification += (_, args) =>
        {
            _logger.LogDebug("Received pg_notify on channel '{Channel}' — triggering immediate batch", args.Channel);
        };

        await using (var cmd = new NpgsqlCommand($"LISTEN {NotifyChannel}", conn))
        {
            await cmd.ExecuteNonQueryAsync(stoppingToken);
        }

        _logger.LogDebug("Listening on pg_notify channel '{Channel}'", NotifyChannel);

        while (!stoppingToken.IsCancellationRequested)
        {
            // WaitAsync returns when a notification arrives OR the timeout elapses.
            // We use a short timeout so we don't block indefinitely while waiting for
            // the cancellation token to be honoured, but we don't expect it to be the
            // primary trigger — pg_notify fires as soon as the transaction commits.
            await conn.WaitAsync(stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox batch after pg_notify");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Fallback poller — catches any notifications that were missed
    // -------------------------------------------------------------------------

    private async Task RunFallbackPollerAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_options.FallbackPollingIntervalMs, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox events in fallback polling batch");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Core batch processor — shared by both the listener and the fallback poller
    // -------------------------------------------------------------------------

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;

        // Query unprocessed events with exponential backoff filter pushed to SQL.
        // For first attempts (RetryCount == 0): always eligible.
        // For retries: eligible when seconds since last attempt >= 2^RetryCount.
        var events = await context.OutboxEvents
            .Where(e => e.ProcessedAt == null)
            .Where(e => e.RetryCount == 0
                     || (now - (e.LastAttemptAt ?? e.CreatedAt)).TotalSeconds >= Math.Pow(2, e.RetryCount))
            .OrderBy(e => e.CreatedAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Processing {EventCount} outbox events", events.Count);

        foreach (var evt in events)
        {
            try
            {
                if (_eventDispatcher != null)
                {
                    // Dispatch via SignalR
                    await _eventDispatcher.DispatchAsync(evt.EventType, evt.Payload);
                }
                else
                {
                    // Fallback logging if no dispatcher is registered
                    _logger.LogInformation(
                        "Dispatching outbox event {EventId} of type {EventType}: {Payload}",
                        evt.Id, evt.EventType, evt.Payload);
                }

                // Mark as processed
                evt.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching outbox event {EventId}", evt.Id);

                // Increment retry count and record attempt time
                evt.RetryCount++;
                evt.LastAttemptAt = DateTimeOffset.UtcNow;

                // If max retries exceeded, mark as processed to avoid infinite retry
                if (evt.RetryCount > _options.MaxRetryCount)
                {
                    _logger.LogError(
                        "Outbox event {EventId} exceeded max retry count ({MaxRetryCount}), marking as processed",
                        evt.Id, _options.MaxRetryCount);
                    evt.ProcessedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
