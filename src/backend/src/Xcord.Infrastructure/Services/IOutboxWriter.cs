using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for writing events to the outbox in the same transaction as domain changes.
/// </summary>
public interface IOutboxWriter
{
    /// <summary>
    /// Writes an event to the outbox using the provided DbContext.
    /// The event will be saved in the SAME transaction as the caller's SaveChangesAsync.
    /// </summary>
    /// <param name="context">The DbContext to use (ensures same transaction).</param>
    /// <param name="eventType">The event type (e.g., "Message.Created").</param>
    /// <param name="payload">The event payload to serialize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteAsync(AppDbContext context, string eventType, object payload, CancellationToken cancellationToken = default);
}
