using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Threads;

public sealed record LeaveThreadRequest(
    long ChannelId,
    long ThreadId
);

public sealed class LeaveThreadHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<LeaveThreadHandler> logger)
    : IRequestHandler<LeaveThreadRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(LeaveThreadRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get thread to verify it exists
        var threadExists = await dbContext.Threads
            .AsNoTracking()
            .AnyAsync(t => t.Id == request.ThreadId && t.ChannelId == request.ChannelId, cancellationToken);

        if (!threadExists)
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

        // Find and remove thread member
        var threadMember = await dbContext.ThreadMembers
            .FirstOrDefaultAsync(tm => tm.UserId == userId && tm.ThreadId == request.ThreadId, cancellationToken);

        if (threadMember != null)
        {
            dbContext.ThreadMembers.Remove(threadMember);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "User {UserId} left thread {ThreadId}",
                userId, request.ThreadId);
        }

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/channels/{channelId}/threads/{threadId}/members/@me", async (
            long channelId,
            long threadId,
            [FromServices] LeaveThreadHandler handler,
            CancellationToken ct) =>
        {
            var request = new LeaveThreadRequest(
                ChannelId: channelId,
                ThreadId: threadId
            );

            return await handler.ExecuteAsync(request, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("LeaveThread")
        .WithTags("Threads");
    }
}
