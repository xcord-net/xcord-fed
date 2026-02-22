namespace Xcord.Features.Notes;

public sealed record UserNoteDto(
    long Id,
    long TargetUserId,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
