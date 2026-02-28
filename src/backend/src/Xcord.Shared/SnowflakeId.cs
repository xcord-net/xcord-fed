using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xcord;

/// <summary>
/// 64-bit Snowflake ID generator.
/// Format: 41-bit timestamp (ms since epoch) + 10-bit workerId + 12-bit sequence.
/// </summary>
public sealed class SnowflakeIdGenerator
{
    private static readonly DateTimeOffset DefaultEpoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const int WorkerIdBits = 10;
    private const int SequenceBits = 12;

    private const int MaxWorkerId = (1 << WorkerIdBits) - 1; // 1023
    private const int MaxSequence = (1 << SequenceBits) - 1; // 4095

    private const int WorkerIdShift = SequenceBits;
    private const int TimestampShift = SequenceBits + WorkerIdBits;

    private readonly DateTimeOffset _epoch;
    private readonly int _workerId;
    private readonly object _lock = new();

    private long _lastTimestamp = -1;
    private int _sequence = 0;

    public SnowflakeIdGenerator(int workerId, DateTimeOffset? epoch = null)
    {
        if (workerId < 0 || workerId > MaxWorkerId)
        {
            throw new ArgumentOutOfRangeException(nameof(workerId),
                $"Worker ID must be between 0 and {MaxWorkerId}");
        }

        _workerId = workerId;
        _epoch = epoch ?? DefaultEpoch;
    }

    public long NextId()
    {
        lock (_lock)
        {
            var timestamp = GetTimestamp();

            if (timestamp < _lastTimestamp)
            {
                throw new InvalidOperationException("Clock moved backwards. Refusing to generate ID.");
            }

            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & MaxSequence;

                if (_sequence == 0)
                {
                    // Sequence overflow, wait for next millisecond
                    timestamp = WaitNextMillisecond(timestamp);
                }
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestamp = timestamp;

            return (timestamp << TimestampShift)
                   | ((long)_workerId << WorkerIdShift)
                   | (long)_sequence;
        }
    }

    private long GetTimestamp()
    {
        return (long)(DateTimeOffset.UtcNow - _epoch).TotalMilliseconds;
    }

    private long WaitNextMillisecond(long currentTimestamp)
    {
        var timestamp = GetTimestamp();
        while (timestamp <= currentTimestamp)
        {
            timestamp = GetTimestamp();
        }
        return timestamp;
    }
}

/// <summary>
/// JSON converter for Snowflake IDs to serialize as strings.
/// Registered globally — affects ALL long fields. Use <see cref="LongAsNumberConverter"/>
/// on non-ID long properties (e.g. permission bitmasks, file sizes) to override.
/// </summary>
public sealed class SnowflakeJsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (long.TryParse(str, out var value))
            {
                return value;
            }
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64();
        }

        throw new JsonException("Expected string or number for Snowflake ID");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// JSON converter that serializes long values as numbers (not strings).
/// Apply via [property: JsonConverter(typeof(LongAsNumberConverter))] on non-ID long
/// fields like permission bitmasks and file sizes to override the global
/// SnowflakeJsonConverter which stringifies all longs.
/// </summary>
public sealed class LongAsNumberConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetInt64();

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (long.TryParse(str, out var value))
                return value;
        }

        throw new JsonException("Expected number or numeric string");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

/// <summary>
/// Nullable variant of <see cref="LongAsNumberConverter"/> for long? fields.
/// </summary>
public sealed class NullableLongAsNumberConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetInt64();

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (long.TryParse(str, out var value))
                return value;
        }

        throw new JsonException("Expected number, numeric string, or null");
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
