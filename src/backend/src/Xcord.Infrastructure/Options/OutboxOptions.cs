using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>
    /// Retention period for processed events in minutes (default: 60 minutes).
    /// </summary>
    [Range(1, 10080)]
    public int RetentionMinutes { get; set; } = 60;

    /// <summary>
    /// Cleanup interval in minutes (default: 5 minutes).
    /// </summary>
    [Range(1, 1440)]
    public int CleanupIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum retry count before marking as processed (default: 10).
    /// </summary>
    [Range(1, 100)]
    public int MaxRetryCount { get; set; } = 10;

    /// <summary>
    /// Batch size for polling unprocessed events (default: 50).
    /// </summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Fallback polling interval in milliseconds when pg_notify is active (default: 30000ms / 30s).
    /// This longer interval is used as a safety net in case a notification is missed.
    /// </summary>
    [Range(1000, 300000)]
    public int FallbackPollingIntervalMs { get; set; } = 30000;
}
