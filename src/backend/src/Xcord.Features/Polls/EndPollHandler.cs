using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Polls;

public sealed record EndPollCommand(
    long PollId
);

public sealed record EndPollResponse(
    long PollId,
    bool IsClosed
);

public sealed class EndPollHandler(
    AppDbContext dbContext,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    ILogger<EndPollHandler> logger)
    : IRequestHandler<EndPollCommand, Result<EndPollResponse>>
{
    public async Task<Result<EndPollResponse>> Handle(EndPollCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get poll with message
        var poll = await dbContext.Polls
            .Include(p => p.Message)
                .ThenInclude(m => m.Conversation)
            .FirstOrDefaultAsync(p => p.Id == request.PollId, cancellationToken);

        if (poll == null)
        {
            return Error.NotFound("POLL_NOT_FOUND", "Poll not found");
        }

        if (poll.IsClosed)
        {
            return Error.Validation("POLL_ALREADY_CLOSED", "Poll is already closed");
        }

        // Verify user is the poll creator OR has ManageMessages permission
        var isCreator = poll.Message.AuthorId == userId;
        var hasManagePermission = false;

        var conversation = poll.Message.Conversation;

        if (conversation.Type == ConversationType.Channel)
        {
            var channel = await dbContext.Channels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ConversationId == conversation.Id, cancellationToken);

            if (channel != null)
            {
                var channelPerms = await roleService.GetChannelRoles(userId, channel.Id).ConfigureAwait(false);
                hasManagePermission = (channelPerms & (long)Role.ManageMessages) != 0;
            }
        }
        else if (conversation.Type == ConversationType.Thread)
        {
            var thread = await dbContext.Threads
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ConversationId == conversation.Id, cancellationToken);

            if (thread != null)
            {
                var channelPerms = await roleService.GetChannelRoles(userId, thread.ChannelId).ConfigureAwait(false);
                hasManagePermission = (channelPerms & (long)Role.ManageMessages) != 0;
            }
        }

        if (!isCreator && !hasManagePermission)
        {
            return Error.Forbidden("INSUFFICIENT_PERMISSIONS", "You do not have permission to end this poll");
        }

        // Begin transaction
        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            poll.IsClosed = true;

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Notify after save
            await notificationService.NotifyConversationAsync(poll.Message.ConversationId, "Poll_Ended", new
            {
                PollId = poll.Id,
                ConversationId = poll.Message.ConversationId
            }, cancellationToken);

            logger.LogInformation(
                "User {UserId} ended poll {PollId}",
                userId, request.PollId);

            return new EndPollResponse(
                PollId: poll.Id,
                IsClosed: poll.IsClosed
            );
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/polls/{pollId}/end", async (
            long pollId,
            IRequestHandler<EndPollCommand, Result<EndPollResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new EndPollCommand(PollId: pollId);
            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("EndPoll")
        .WithTags("Polls");
    }
}
