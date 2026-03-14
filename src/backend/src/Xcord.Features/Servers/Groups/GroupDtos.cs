using System.Text.Json.Serialization;

namespace Xcord.Features.Servers;

public sealed record GroupDto(
    long Id,
    long ServerId,
    string Name,
    string? Color,
    [property: JsonConverter(typeof(LongAsNumberConverter))] long Roles,
    int Position,
    bool IsEveryone,
    string? LimitsJson,
    DateTimeOffset CreatedAt
);

public sealed record CreateGroupRequest(
    string Name,
    string? Color,
    [property: JsonConverter(typeof(LongAsNumberConverter))] long Roles,
    int Position,
    string? LimitsJson
);

public sealed record UpdateGroupRequest(
    string? Name,
    string? Color,
    [property: JsonConverter(typeof(NullableLongAsNumberConverter))] long? Roles,
    int? Position,
    string? LimitsJson
);
