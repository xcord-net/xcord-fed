using System.Text.Json;
using System.Text.Json.Serialization;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Implementation of IOutboxWriter for writing events to the outbox.
/// </summary>
public sealed class OutboxWriter : IOutboxWriter
{
    private readonly SnowflakeIdGenerator _snowflakeGenerator;

    /// <summary>
    /// Shared serializer options that match the API's HTTP JSON config:
    /// Snowflake IDs (long) serialize as JSON strings and enums serialize as strings.
    /// This ensures clients receive IDs and enums in the same format they expect from
    /// REST API responses — no normalization needed in frontend stores.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters =
        {
            new SnowflakeJsonConverter(),
            new JsonStringEnumConverter(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public OutboxWriter(SnowflakeIdGenerator snowflakeGenerator)
    {
        _snowflakeGenerator = snowflakeGenerator;
    }

    public Task WriteAsync(AppDbContext context, string eventType, object payload, CancellationToken cancellationToken = default)
    {
        var outboxEvent = new OutboxEvent
        {
            Id = _snowflakeGenerator.NextId(),
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload, SerializerOptions),
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null,
            RetryCount = 0
        };

        context.OutboxEvents.Add(outboxEvent);

        // Note: SaveChangesAsync is NOT called here - the caller is responsible
        // This ensures the event is written in the SAME transaction as domain changes
        return Task.CompletedTask;
    }
}
