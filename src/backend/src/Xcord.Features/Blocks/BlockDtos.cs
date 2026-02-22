namespace Xcord.Features.Blocks;

public sealed record UserBlockDto(
    long BlockerId,
    long BlockedId,
    string BlockedUsername,
    string BlockedDisplayName,
    string? BlockedAvatarUrl,
    DateTimeOffset CreatedAt
);
