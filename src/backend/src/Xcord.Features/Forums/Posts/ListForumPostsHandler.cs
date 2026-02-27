using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Forums;

public sealed record ListForumPostsCommand(
    long ChannelId,
    ForumSort? Sort = null,
    long? TagId = null,
    bool? Archived = false
);

public sealed record ListForumPostsResponse(
    List<ForumPostDto> Posts
);

public sealed record ForumPostDto(
    long ThreadId,
    long ConversationId,
    long ChannelId,
    string Title,
    long? AuthorId,
    string? AuthorUsername,
    string? FirstMessagePreview,
    List<string> Tags,
    int MessageCount,
    bool IsArchived,
    bool IsLocked,
    DateTimeOffset LastActivityAt,
    DateTimeOffset CreatedAt
);

public sealed class ListForumPostsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListForumPostsCommand, Result<ListForumPostsResponse>>
{
    public async Task<Result<ListForumPostsResponse>> Handle(ListForumPostsCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        if (channel.Type != ChannelType.Forum)
        {
            return Error.Validation("NOT_FORUM_CHANNEL", "Channel is not a forum channel");
        }

        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == channel.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_MEMBER", "User is not a member of this server");
        }

        var query = dbContext.Threads
            .AsNoTracking()
            .Where(t => t.ChannelId == request.ChannelId);

        if (request.Archived.HasValue)
        {
            query = query.Where(t => t.IsArchived == request.Archived.Value);
        }

        if (request.TagId.HasValue)
        {
            var threadIdsWithTag = await dbContext.ForumPostTags
                .AsNoTracking()
                .Where(fpt => fpt.ForumTagId == request.TagId.Value)
                .Select(fpt => fpt.ThreadId)
                .ToListAsync(cancellationToken);

            query = query.Where(t => threadIdsWithTag.Contains(t.Id));
        }

        var sortOrder = request.Sort ?? channel.DefaultSortOrder ?? ForumSort.LatestActivity;

        query = sortOrder switch
        {
            ForumSort.LatestActivity => query.OrderByDescending(t => t.LastActivityAt),
            ForumSort.CreationDate => query.OrderByDescending(t => t.CreatedAt),
            _ => query.OrderByDescending(t => t.LastActivityAt)
        };

        var threads = await query.ToListAsync(cancellationToken);

        var threadIds = threads.Select(t => t.Id).ToList();
        var conversationIds = threads.Select(t => t.ConversationId).ToList();

        // Fetch first messages (with author) for each thread conversation
        var firstMessageInfos = await dbContext.Messages
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .Join(dbContext.Users.AsNoTracking(),
                m => m.AuthorId,
                u => u.Id,
                (m, u) => new { m.Id, m.ConversationId, m.AuthorId, AuthorUsername = u.Username, m.Content, m.CreatedAt })
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderBy(m => m.CreatedAt).First())
            .ToListAsync(cancellationToken);

        var firstMessages = firstMessageInfos.ToDictionary(m => m.ConversationId);

        var postTagNames = await dbContext.ForumPostTags
            .AsNoTracking()
            .Where(fpt => threadIds.Contains(fpt.ThreadId))
            .Join(dbContext.ForumTags.AsNoTracking(),
                fpt => fpt.ForumTagId,
                ft => ft.Id,
                (fpt, ft) => new { fpt.ThreadId, ft.Name })
            .GroupBy(x => x.ThreadId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(x => x.Name).ToList(),
                cancellationToken);

        var posts = threads.Select(t =>
        {
            var firstMessage = firstMessages.GetValueOrDefault(t.ConversationId);
            var preview = firstMessage?.Content?.Length > 200
                ? firstMessage.Content.Substring(0, 200) + "..."
                : firstMessage?.Content;

            return new ForumPostDto(
                ThreadId: t.Id,
                ConversationId: t.ConversationId,
                ChannelId: t.ChannelId,
                Title: t.Title ?? string.Empty,
                AuthorId: firstMessage?.AuthorId,
                AuthorUsername: firstMessage?.AuthorUsername,
                FirstMessagePreview: preview,
                Tags: postTagNames.GetValueOrDefault(t.Id, new List<string>()),
                MessageCount: t.MessageCount,
                IsArchived: t.IsArchived,
                IsLocked: t.IsLocked,
                LastActivityAt: t.LastActivityAt,
                CreatedAt: t.CreatedAt
            );
        }).ToList();

        return new ListForumPostsResponse(Posts: posts);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/channels/{channelId}/posts", async (
            long channelId,
            string? sort,
            long? tag,
            bool? archived,
            IRequestHandler<ListForumPostsCommand, Result<ListForumPostsResponse>> handler,
            CancellationToken ct) =>
        {
            ForumSort? sortOrder = null;
            if (!string.IsNullOrEmpty(sort))
            {
                sortOrder = sort.ToLowerInvariant() switch
                {
                    "latest_activity" => ForumSort.LatestActivity,
                    "creation_date" => ForumSort.CreationDate,
                    _ => null
                };
            }

            var command = new ListForumPostsCommand(
                ChannelId: channelId,
                Sort: sortOrder,
                TagId: tag,
                Archived: archived
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListForumPosts")
        .WithTags("Forums");
    }
}
