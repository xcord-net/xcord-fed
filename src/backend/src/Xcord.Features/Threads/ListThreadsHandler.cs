using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Threads;

public sealed record ListThreadsRequest(
    long ChannelId,
    bool Archived = false
);

public sealed record ListThreadsResponse(
    List<ThreadSummary> Threads
);

public sealed record ThreadSummary(
    long Id,
    long ConversationId,
    long ChannelId,
    long? ParentMessageId,
    string? Title,
    bool IsArchived,
    bool IsLocked,
    int AutoArchiveDurationMinutes,
    DateTimeOffset LastActivityAt,
    int MessageCount,
    int MemberCount,
    DateTimeOffset CreatedAt
);

public sealed class ListThreadsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListThreadsRequest, Result<ListThreadsResponse>>
{
    public async Task<Result<ListThreadsResponse>> Handle(ListThreadsRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get channel to verify it exists and get server ID
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        // Verify user is a member of the server
        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == channel.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_MEMBER", "User is not a member of this server");
        }

        // Get threads filtered by archived status
        var threads = await dbContext.Threads
            .AsNoTracking()
            .Where(t => t.ChannelId == request.ChannelId && t.IsArchived == request.Archived)
            .OrderByDescending(t => t.LastActivityAt)
            .ToListAsync(cancellationToken);

        // Get member counts for all threads
        var threadIds = threads.Select(t => t.Id).ToList();
        var memberCounts = await dbContext.ThreadMembers
            .AsNoTracking()
            .Where(tm => threadIds.Contains(tm.ThreadId))
            .GroupBy(tm => tm.ThreadId)
            .Select(g => new { ThreadId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ThreadId, x => x.Count, cancellationToken);

        var threadSummaries = threads.Select(t => new ThreadSummary(
            Id: t.Id,
            ConversationId: t.ConversationId,
            ChannelId: t.ChannelId,
            ParentMessageId: t.ParentMessageId,
            Title: t.Title,
            IsArchived: t.IsArchived,
            IsLocked: t.IsLocked,
            AutoArchiveDurationMinutes: t.AutoArchiveDurationMinutes,
            LastActivityAt: t.LastActivityAt,
            MessageCount: t.MessageCount,
            MemberCount: memberCounts.GetValueOrDefault(t.Id, 0),
            CreatedAt: t.CreatedAt
        )).ToList();

        return new ListThreadsResponse(threadSummaries);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/channels/{channelId}/threads", async (
            long channelId,
            bool archived,
            [FromServices] ListThreadsHandler handler,
            CancellationToken ct) =>
        {
            var request = new ListThreadsRequest(
                ChannelId: channelId,
                Archived: archived
            );

            return await handler.ExecuteAsync(request, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListThreads")
        .WithTags("Threads");
    }
}
