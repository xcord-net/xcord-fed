using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Threads;

public sealed record GetThreadRequest(
    long ChannelId,
    long ThreadId
);

public sealed record GetThreadResponse(
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

public sealed class GetThreadHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<GetThreadRequest, Result<GetThreadResponse>>
{
    public async Task<Result<GetThreadResponse>> Handle(GetThreadRequest request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Get thread
        var thread = await dbContext.Threads
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.ThreadId && t.ChannelId == request.ChannelId, cancellationToken);

        if (thread == null)
        {
            return Error.NotFound("THREAD_NOT_FOUND", "Thread not found");
        }

        // Get channel to verify server membership
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

        // Count thread members
        var memberCount = await dbContext.ThreadMembers
            .AsNoTracking()
            .CountAsync(tm => tm.ThreadId == request.ThreadId, cancellationToken);

        return new GetThreadResponse(
            Id: thread.Id,
            ConversationId: thread.ConversationId,
            ChannelId: thread.ChannelId,
            ParentMessageId: thread.ParentMessageId,
            Title: thread.Title,
            IsArchived: thread.IsArchived,
            IsLocked: thread.IsLocked,
            AutoArchiveDurationMinutes: thread.AutoArchiveDurationMinutes,
            LastActivityAt: thread.LastActivityAt,
            MessageCount: thread.MessageCount,
            MemberCount: memberCount,
            CreatedAt: thread.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/channels/{channelId}/threads/{threadId}", async (
            long channelId,
            long threadId,
            [FromServices] GetThreadHandler handler,
            CancellationToken ct) =>
        {
            var request = new GetThreadRequest(
                ChannelId: channelId,
                ThreadId: threadId
            );

            return await handler.ExecuteAsync(request, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetThread")
        .WithTags("Threads");
    }
}
