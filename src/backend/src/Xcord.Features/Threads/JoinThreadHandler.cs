using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Threads;

public sealed record JoinThreadRequest(
    long ChannelId,
    long ThreadId
);

public sealed record JoinThreadResponse(
    long ThreadId,
    long UserId,
    DateTimeOffset JoinedAt
);

public sealed class JoinThreadHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<JoinThreadHandler> logger)
    : IRequestHandler<JoinThreadRequest, Result<JoinThreadResponse>>
{
    public async Task<Result<JoinThreadResponse>> Handle(JoinThreadRequest request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Get thread
        var thread = await dbContext.Threads
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

        // Check if already a member
        var alreadyMember = await dbContext.ThreadMembers
            .AsNoTracking()
            .AnyAsync(tm => tm.UserId == userId && tm.ThreadId == request.ThreadId, cancellationToken);

        if (alreadyMember)
        {
            // Return success - already joined
            var existingMember = await dbContext.ThreadMembers
                .AsNoTracking()
                .FirstAsync(tm => tm.UserId == userId && tm.ThreadId == request.ThreadId, cancellationToken);

            return new JoinThreadResponse(
                ThreadId: request.ThreadId,
                UserId: userId,
                JoinedAt: existingMember.JoinedAt
            );
        }

        var now = DateTimeOffset.UtcNow;

        // Add thread member
        var threadMember = new ThreadMember
        {
            UserId = userId,
            ThreadId = request.ThreadId,
            JoinedAt = now
        };

        dbContext.ThreadMembers.Add(threadMember);

        // Update thread's LastActivityAt
        thread.LastActivityAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} joined thread {ThreadId}",
            userId, request.ThreadId);

        return new JoinThreadResponse(
            ThreadId: request.ThreadId,
            UserId: userId,
            JoinedAt: now
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/channels/{channelId}/threads/{threadId}/members", async (
            long channelId,
            long threadId,
            [FromServices] JoinThreadHandler handler,
            CancellationToken ct) =>
        {
            var request = new JoinThreadRequest(
                ChannelId: channelId,
                ThreadId: threadId
            );

            return await handler.ExecuteAsync(request, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("JoinThread")
        .WithTags("Threads");
    }
}
