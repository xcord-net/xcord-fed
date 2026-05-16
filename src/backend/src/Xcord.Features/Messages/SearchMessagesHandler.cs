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

public sealed record SearchMessagesRequest(
    string? Query,
    long? ConversationId,
    bool? HasLink,
    bool? HasAttachment,
    string? Cursor,
    int Limit = 25
);

public sealed record SearchMessagesResponse(
    List<MessageDto> Messages,
    bool HasMore,
    string? NextCursor = null
);

public sealed class SearchMessagesHandler(
    AppDbContext dbContext,
    IConversationResolver conversationResolver,
    ICurrentUserService currentUserService,
    ICursorService cursorService)
    : IRequestHandler<SearchMessagesRequest, Result<SearchMessagesResponse>>, IValidatable<SearchMessagesRequest>
{
    public Error? Validate(SearchMessagesRequest request)
    {
        if (request.Query != null && string.IsNullOrWhiteSpace(request.Query))
            return Error.Validation("VALIDATION_FAILED", "Query must not be empty when provided");

        if (request.Limit < 1)
            return Error.Validation("VALIDATION_FAILED", "Limit must be at least 1");

        if (request.Limit > 100)
            return Error.Validation("VALIDATION_FAILED", "Limit must not exceed 100");

        return null;
    }

    public async Task<Result<SearchMessagesResponse>> Handle(SearchMessagesRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Decode opaque cursor (returns null when no cursor was supplied)
        var cursorResult = cursorService.Decode(request.Cursor);
        if (cursorResult.IsFailure) return cursorResult.Error;
        var beforeId = cursorResult.Value;

        List<long> allowedConversationIds;

        if (request.ConversationId.HasValue)
        {
            // Verify the user has ReadMessageHistory permission on the specified conversation
            var contextResult = await conversationResolver.ResolveAsync(
                request.ConversationId.Value, userId, Role.ReadMessageHistory, cancellationToken);
            if (contextResult.IsFailure) return contextResult.Error;

            allowedConversationIds = [request.ConversationId.Value];
        }
        else
        {
            // Build list of conversation IDs from server channels the user is a member of
            var serverConversationIds = await dbContext.ServerMembers
                .AsNoTracking()
                .Where(sm => sm.UserId == userId)
                .Join(dbContext.Channels,
                    sm => sm.ServerId,
                    ch => ch.ServerId,
                    (sm, ch) => ch.ConversationId)
                .ToListAsync(cancellationToken);

            // Build list of conversation IDs from DM channels the user is a member of
            var dmConversationIds = await dbContext.DmChannelMembers
                .AsNoTracking()
                .Where(dm => dm.UserId == userId)
                .Join(dbContext.DmChannels,
                    dm => dm.DmChannelId,
                    dc => dc.Id,
                    (dm, dc) => dc.ConversationId)
                .ToListAsync(cancellationToken);

            allowedConversationIds = serverConversationIds
                .Union(dmConversationIds)
                .ToList();
        }

        // Build base query filtered to allowed conversations
        var query = dbContext.Messages
            .AsNoTracking()
            .Where(m => allowedConversationIds.Contains(m.ConversationId));

        // Apply text search filter
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var searchTerm = $"%{request.Query}%";
            query = query.Where(m => EF.Functions.ILike(m.Content, searchTerm));
        }

        // Apply HasLink filter
        if (request.HasLink == true)
        {
            query = query.Where(m => EF.Functions.ILike(m.Content, "%http%"));
        }

        // Apply cursor-based pagination
        if (beforeId.HasValue)
        {
            query = query.Where(m => m.Id < beforeId.Value);
        }

        // Fetch one extra to determine HasMore
        var fetchLimit = request.Limit + 1;

        var messages = await query
            .OrderByDescending(m => m.Id)
            .Take(fetchLimit)
            .Select(m => new
            {
                Message = m,
                Author = m.Author
            })
            .ToListAsync(cancellationToken);

        var hasMore = messages.Count > request.Limit;
        if (hasMore)
        {
            messages = messages.Take(request.Limit).ToList();
        }

        var messageDtos = messages.Select(m => new MessageDto(
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
            CreatedAt: m.Message.CreatedAt
        )).ToList();

        // Encode the next cursor when there are more results to fetch
        var nextCursor = hasMore && messageDtos.Count > 0
            ? cursorService.Encode(messageDtos[^1].Id)
            : null;

        return new SearchMessagesResponse(Messages: messageDtos, HasMore: hasMore, NextCursor: nextCursor);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/search", async (
            string? query,
            long? conversationId,
            bool? hasLink,
            bool? hasAttachment,
            string? cursor,
            int? limit,
            [FromServices] SearchMessagesHandler handler,
            CancellationToken ct) =>
        {
            var request = new SearchMessagesRequest(
                Query: query,
                ConversationId: conversationId,
                HasLink: hasLink,
                HasAttachment: hasAttachment,
                Cursor: cursor,
                Limit: limit ?? 25
            );
            return await handler.ExecuteAsync(request, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("SearchMessages")
        .WithTags("Messages");
}
