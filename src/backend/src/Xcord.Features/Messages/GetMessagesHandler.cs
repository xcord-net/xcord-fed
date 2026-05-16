using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Messages;

public sealed record GetMessagesRequest(
    long ConversationId,
    string? Cursor = null,
    int Limit = 50
);

public sealed record GetMessagesResponse(
    List<MessageDto> Messages,
    string? NextCursor = null
);

public sealed record MessageDto(
    long Id,
    long ConversationId,
    long? AuthorId,
    string? AuthorUsername,
    string? AuthorAvatarUrl,
    MessageType Type,
    string Content,
    string? Metadata,
    long? ReplyToId,
    bool IsPinned,
    DateTimeOffset? EditedAt,
    DateTimeOffset CreatedAt,
    List<MessageReactionDto>? Reactions = null,
    List<AttachmentDto>? Attachments = null,
    long? PollId = null
);

public sealed record AttachmentDto(
    long Id,
    string FileName,
    string ContentType,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(LongAsNumberConverter))] long FileSize,
    int? Width,
    int? Height,
    string DownloadUrl,
    string? ThumbnailUrl
);

public sealed record MessageReactionDto(
    string Emoji,
    int Count,
    List<long> UserIds
);

public sealed class GetMessagesHandler(
    AppDbContext dbContext,
    IConversationResolver conversationResolver,
    ICurrentUserService currentUserService,
    IStorageService storageService,
    ICursorService cursorService)
    : IRequestHandler<GetMessagesRequest, Result<GetMessagesResponse>>, IValidatable<GetMessagesRequest>
{
    public Error? Validate(GetMessagesRequest request)
    {
        if (request.Limit <= 0)
            return Error.Validation("VALIDATION_FAILED", "Limit must be greater than 0");

        if (request.Limit > 100)
            return Error.Validation("VALIDATION_FAILED", "Limit must not exceed 100");

        return null;
    }

    public async Task<Result<GetMessagesResponse>> Handle(GetMessagesRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Decode opaque cursor (returns null when no cursor was supplied)
        var cursorResult = cursorService.Decode(request.Cursor);
        if (cursorResult.IsFailure) return cursorResult.Error;
        var beforeId = cursorResult.Value;

        // Resolve conversation and check permissions
        var contextResult = await conversationResolver.ResolveAsync(
            request.ConversationId, userId, Role.ReadMessageHistory, cancellationToken);
        if (contextResult.IsFailure) return contextResult.Error;

        // Build query with cursor-based pagination
        var query = dbContext.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.ConversationId);

        // Apply cursor if provided (messages before the given ID)
        if (beforeId.HasValue)
        {
            query = query.Where(m => m.Id < beforeId.Value);
        }

        // Order by ID descending (newest first) and take limit
        var messages = await query
            .OrderByDescending(m => m.Id)
            .Take(request.Limit)
            .Select(m => new
            {
                Message = m,
                Author = m.Author,
                Reactions = m.Reactions.GroupBy(r => r.Emoji).Select(g => new MessageReactionDto(
                    g.Key,
                    g.Count(),
                    g.Select(r => r.UserId).ToList()
                )).ToList(),
                Attachments = m.Attachments
                    .Where(a => a.IsConfirmed && a.DeletedAt == null)
                    .ToList(),
                PollId = dbContext.Polls
                    .Where(p => p.MessageId == m.Id && p.DeletedAt == null)
                    .Select(p => (long?)p.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Generate pre-signed URLs for all attachments across all messages.
        // Build a flat list of (messageId, attachment) pairs, then resolve URLs in parallel.
        var urlTasks = messages
            .SelectMany(m => m.Attachments.Select(a => (MessageId: m.Message.Id, Attachment: a)))
            .Select(async pair =>
            {
                var downloadUrl = await storageService.GenerateDownloadUrlAsync(pair.Attachment.S3Key, TimeSpan.FromHours(1)).ConfigureAwait(false);

                // Empty string is a sentinel meaning "not applicable" (non-image).
                string? thumbnailUrl = null;
                if (!string.IsNullOrEmpty(pair.Attachment.ThumbnailS3Key))
                {
                    thumbnailUrl = await storageService.GenerateDownloadUrlAsync(pair.Attachment.ThumbnailS3Key, TimeSpan.FromHours(1)).ConfigureAwait(false);
                }

                return (pair.MessageId, pair.Attachment, DownloadUrl: downloadUrl, ThumbnailUrl: thumbnailUrl);
            })
            .ToList();

        var resolvedUrls = await Task.WhenAll(urlTasks).ConfigureAwait(false);

        // Group resolved attachments by message ID for fast lookup.
        var attachmentsByMessage = resolvedUrls
            .GroupBy(x => x.MessageId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new AttachmentDto(
                    Id: x.Attachment.Id,
                    FileName: x.Attachment.FileName,
                    ContentType: x.Attachment.ContentType,
                    FileSize: x.Attachment.FileSize,
                    Width: x.Attachment.Width,
                    Height: x.Attachment.Height,
                    DownloadUrl: x.DownloadUrl,
                    ThumbnailUrl: x.ThumbnailUrl
                )).ToList()
            );

        // Map to DTOs
        var messageDtos = messages.Select(m =>
        {
            attachmentsByMessage.TryGetValue(m.Message.Id, out var attachmentDtos);
            return new MessageDto(
                Id: m.Message.Id,
                ConversationId: m.Message.ConversationId,
                AuthorId: m.Message.AuthorId,
                AuthorUsername: m.Author != null ? m.Author.Username : null,
                AuthorAvatarUrl: m.Author != null ? m.Author.AvatarUrl : null,
                Type: m.Message.Type,
                Content: m.Message.Content,
                Metadata: m.Message.Metadata,
                ReplyToId: m.Message.ReplyToId,
                IsPinned: m.Message.IsPinned,
                EditedAt: m.Message.EditedAt,
                CreatedAt: m.Message.CreatedAt,
                Reactions: m.Reactions.Count > 0 ? m.Reactions : null,
                Attachments: attachmentDtos is { Count: > 0 } ? attachmentDtos : null,
                PollId: m.PollId
            );
        }).ToList();

        // Build NextCursor from the oldest returned message (newest-first ordering means
        // the last item is the oldest; the next page should fetch messages older than that).
        // If the page came back smaller than the limit, there are no more older messages.
        string? nextCursor = null;
        if (messageDtos.Count == request.Limit && messageDtos.Count > 0)
        {
            var oldestId = messageDtos[^1].Id;
            nextCursor = cursorService.Encode(oldestId);
        }

        return new GetMessagesResponse(Messages: messageDtos, NextCursor: nextCursor);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/conversations/{conversationId}/messages", async (
            long conversationId,
            string? cursor,
            int? limit,
            [FromServices] GetMessagesHandler handler,
            CancellationToken ct) =>
        {
            var request = new GetMessagesRequest(
                ConversationId: conversationId,
                Cursor: cursor,
                Limit: limit ?? 50
            );

            return await handler.ExecuteAsync(request, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetMessages")
        .WithTags("Messages");
    }
}
