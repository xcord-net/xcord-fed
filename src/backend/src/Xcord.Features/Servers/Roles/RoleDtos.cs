namespace Xcord.Features.Servers.Roles;

public sealed record RoleDto(
    long Id,
    long ServerId,
    string Name,
    string? Color,
    long Permissions,
    int Position,
    bool IsEveryone,
    DateTimeOffset CreatedAt
);

public sealed record CreateRoleRequest(
    string Name,
    string? Color,
    long Permissions,
    int Position
);

public sealed record UpdateRoleRequest(
    string? Name,
    string? Color,
    long? Permissions,
    int? Position
);
