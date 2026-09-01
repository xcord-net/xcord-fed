using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Messages;

/// <summary>
/// Builds <see cref="MessageDto"/> values for the handlers that return plain message
/// lists - pins and search. Keeps username colours and reply references consistent
/// with the main message list rather than letting those endpoints drift into
/// returning nulls for both.
/// </summary>
/// <remarks>
/// GetMessagesHandler builds its own DTOs because it also carries attachments,
/// reactions and polls; it resolves colours through the same
/// <see cref="AuthorGroupColors"/> helper.
/// </remarks>
public static class MessageDtos
{
    /// <summary>Projection every caller here shares: the message, its author, and its reply target.</summary>
    private static IQueryable<MessageRow> SelectRows(IQueryable<Message> query) =>
        query.Select(m => new MessageRow(
            m,
            m.Author != null ? m.Author.Username : null,
            m.Author != null ? m.Author.AvatarUrl : null,
            m.ReplyTo,
            m.ReplyTo != null && m.ReplyTo.Author != null ? m.ReplyTo.Author.Username : null,
            m.ReplyTo != null ? m.ReplyTo.AuthorId : null));

    private sealed record MessageRow(
        Message Message,
        string? AuthorUsername,
        string? AuthorAvatarUrl,
        Message? ReplyTo,
        string? ReplyToAuthorUsername,
        long? ReplyToAuthorId);

    /// <summary>Materializes a message query into DTOs, with one colour lookup for the whole list.</summary>
    public static async Task<List<MessageDto>> FromQueryAsync(
        AppDbContext dbContext,
        IQueryable<Message> query,
        CancellationToken cancellationToken)
    {
        var rows = await SelectRows(query).ToListAsync(cancellationToken);
        return await BuildAsync(dbContext, rows, cancellationToken);
    }

    /// <summary>
    /// Builds a DTO from an already-loaded entity. The caller must have included
    /// <c>Author</c>, <c>ReplyTo</c> and <c>ReplyTo.Author</c>.
    /// </summary>
    public static async Task<MessageDto> FromEntityAsync(
        AppDbContext dbContext,
        Message message,
        CancellationToken cancellationToken)
    {
        var row = new MessageRow(
            message,
            message.Author?.Username,
            message.Author?.AvatarUrl,
            message.ReplyTo,
            message.ReplyTo?.Author?.Username,
            message.ReplyTo?.AuthorId);

        var dtos = await BuildAsync(dbContext, [row], cancellationToken);
        return dtos[0];
    }

    private static async Task<List<MessageDto>> BuildAsync(
        AppDbContext dbContext,
        IReadOnlyList<MessageRow> rows,
        CancellationToken cancellationToken)
    {
        // Group colour is per-server and a result set can span servers (search), so
        // conversations are mapped back to their server before resolving colours.
        // A conversation with no channel row is a DM, which has no groups.
        var conversationIds = rows.Select(r => r.Message.ConversationId).Distinct().ToList();
        var serverIdByConversation = await dbContext.Channels
            .AsNoTracking()
            .Where(c => conversationIds.Contains(c.ConversationId))
            .Select(c => new { c.ConversationId, c.ServerId })
            .ToDictionaryAsync(c => c.ConversationId, c => c.ServerId, cancellationToken);

        long ServerFor(long conversationId) =>
            serverIdByConversation.GetValueOrDefault(conversationId);

        var pairs = rows
            .SelectMany(r => new[] { r.Message.AuthorId, r.ReplyToAuthorId }
                .Where(id => id.HasValue)
                .Select(id => (ServerFor(r.Message.ConversationId), id!.Value)))
            .Distinct()
            .ToList();

        var groupColors = await AuthorGroupColors.ResolveManyAsync(dbContext, pairs, cancellationToken);

        return rows.Select(r =>
        {
            var serverId = ServerFor(r.Message.ConversationId);
            string? colorFor(long? authorId) =>
                authorId.HasValue ? groupColors.GetValueOrDefault((serverId, authorId.Value)) : null;

            return new MessageDto(
                Id: r.Message.Id,
                ConversationId: r.Message.ConversationId,
                AuthorId: r.Message.AuthorId,
                AuthorUsername: r.AuthorUsername,
                AuthorAvatarUrl: r.AuthorAvatarUrl,
                AuthorGroupColor: colorFor(r.Message.AuthorId),
                Type: r.Message.Type,
                Content: r.Message.Content,
                Metadata: r.Message.Metadata,
                ReplyToId: r.Message.ReplyToId,
                IsPinned: r.Message.IsPinned,
                EditedAt: r.Message.EditedAt,
                CreatedAt: r.Message.CreatedAt,
                ReplyTo: ReplyToDto.Resolve(
                    r.Message.ReplyToId, r.ReplyTo, r.ReplyToAuthorUsername, colorFor(r.ReplyToAuthorId))
            );
        }).ToList();
    }
}
