using System.Text.Json.Serialization;

namespace Xcord.Features.Servers;

public sealed record RoleDto(
    long Id,
    long ServerId,
    string Name,
    string? Color,
    [property: JsonConverter(typeof(LongAsNumberConverter))] long Permissions,
    int Position,
    bool IsEveryone,
    DateTimeOffset CreatedAt
);

public sealed record CreateRoleRequest(
    string Name,
    string? Color,
    [property: JsonConverter(typeof(LongAsNumberConverter))] long Permissions,
    int Position
);

public sealed record UpdateRoleRequest(
    string? Name,
    string? Color,
    [property: JsonConverter(typeof(NullableLongAsNumberConverter))] long? Permissions,
    int? Position
);
