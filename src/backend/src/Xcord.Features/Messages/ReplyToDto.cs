using System.Net;

namespace Xcord.Features.Messages;

/// <summary>
/// The message a reply points at, denormalized onto the reply itself so clients can
/// render a quote line without a second fetch. Preview is truncated server-side so a
/// long parent body is never shipped twice.
/// </summary>
public sealed record ReplyToDto(
    long Id,
    long? AuthorId,
    string? AuthorUsername,
    string Preview,
    bool IsDeleted
)
{
    /// <summary>Maximum preview length before truncation, in characters.</summary>
    public const int PreviewLength = 80;

    /// <summary>
    /// Builds a single-line preview: HTML entities decoded, newlines collapsed to spaces,
    /// and truncated to <see cref="PreviewLength"/> with an ellipsis when cut.
    /// </summary>
    /// <remarks>
    /// Stored content is HTML-encoded by MessageProcessor.SanitizeContent, so a newline
    /// is held as "&amp;#xA;" and an emoji as a numeric reference. Decoding first is what
    /// makes both the flattening and the truncation correct - cutting encoded content at
    /// a fixed length can slice an entity in half. The result is rendered as text (the
    /// client escapes it on the way into the DOM), so decoding here is display-only and
    /// never reaches innerHTML.
    /// </remarks>
    public static string BuildPreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;

        var decoded = WebUtility.HtmlDecode(content);
        var flattened = string.Join(' ', decoded.Split(
            ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (flattened.Length <= PreviewLength) return flattened;

        // Never cut between the halves of a surrogate pair - that would emit a lone
        // surrogate and render as a replacement character.
        var cut = PreviewLength;
        if (char.IsHighSurrogate(flattened[cut - 1])) cut--;

        return string.Concat(flattened.AsSpan(0, cut), "…");
    }

    /// <summary>Builds the DTO from parent fields already materialized by a projection.</summary>
    public static ReplyToDto From(long id, long? authorId, string? authorUsername, string? content)
        => new(id, authorId, authorUsername, BuildPreview(content), IsDeleted: false);

    /// <summary>
    /// The parent is gone. Keeps the id so the reply edge still forms client-side, but
    /// carries no author or content.
    /// </summary>
    public static ReplyToDto Deleted(long id)
        => new(id, AuthorId: null, AuthorUsername: null, Preview: string.Empty, IsDeleted: true);

    /// <summary>
    /// Resolves the DTO for a message that may or may not be a reply. The soft-delete
    /// query filter nulls the navigation when the parent is deleted, so a present
    /// <paramref name="replyToId"/> with a null <paramref name="parent"/> means "gone".
    /// </summary>
    public static ReplyToDto? Resolve(long? replyToId, Entities.Message? parent, string? parentAuthorUsername)
    {
        if (!replyToId.HasValue) return null;
        return parent is null
            ? Deleted(replyToId.Value)
            : From(parent.Id, parent.AuthorId, parentAuthorUsername, parent.Content);
    }
}
