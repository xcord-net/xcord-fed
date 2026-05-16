using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Polls;

public sealed record GetPollQuery(
    long PollId
);

public sealed record GetPollResponse(
    long Id,
    long MessageId,
    string Question,
    bool AllowMultipleAnswers,
    DateTimeOffset? ExpiresAt,
    bool IsClosed,
    List<PollOptionDto> Options,
    List<long>? UserVotes
);

public sealed class GetPollHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetPollQuery, Result<GetPollResponse>>
{
    public async Task<Result<GetPollResponse>> Handle(GetPollQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get poll with options and message
        var poll = await dbContext.Polls
            .AsNoTracking()
            .Include(p => p.Options.OrderBy(o => o.Position))
            .Include(p => p.Message)
                .ThenInclude(m => m.Conversation)
            .FirstOrDefaultAsync(p => p.Id == request.PollId, cancellationToken);

        if (poll == null)
        {
            return Error.NotFound("POLL_NOT_FOUND", "Poll not found");
        }

        // Verify user is a member of the conversation
        var conversation = poll.Message.Conversation;

        if (conversation.Type == ConversationType.DmChannel)
        {
            var isMember = await dbContext.DmChannelMembers
                .AsNoTracking()
                .AnyAsync(dm => dm.DmChannelId == conversation.Id && dm.UserId == userId, cancellationToken);

            if (!isMember)
            {
                return Error.Forbidden("NOT_MEMBER", "You are not a member of this conversation");
            }
        }
        else if (conversation.Type == ConversationType.Channel)
        {
            var channel = await dbContext.Channels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ConversationId == conversation.Id, cancellationToken);

            if (channel == null)
            {
                return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
            }

            var isMember = await dbContext.ServerMembers
                .AsNoTracking()
                .AnyAsync(sm => sm.UserId == userId && sm.ServerId == channel.ServerId, cancellationToken);

            if (!isMember)
            {
                return Error.Forbidden("NOT_MEMBER", "You are not a member of this server");
            }
        }
        else if (conversation.Type == ConversationType.Thread)
        {
            var thread = await dbContext.Threads
                .AsNoTracking()
                .Include(t => t.Channel)
                .FirstOrDefaultAsync(t => t.ConversationId == conversation.Id, cancellationToken);

            if (thread == null)
            {
                return Error.NotFound("THREAD_NOT_FOUND", "Thread not found");
            }

            var isMember = await dbContext.ServerMembers
                .AsNoTracking()
                .AnyAsync(sm => sm.UserId == userId && sm.ServerId == thread.Channel.ServerId, cancellationToken);

            if (!isMember)
            {
                return Error.Forbidden("NOT_MEMBER", "You are not a member of this server");
            }
        }

        // Get user's votes for this poll
        var optionIds = poll.Options.Select(o => o.Id).ToList();
        var userVotes = await dbContext.PollVotes
            .AsNoTracking()
            .Where(pv => optionIds.Contains(pv.PollOptionId) && pv.UserId == userId)
            .Select(pv => pv.PollOptionId)
            .ToListAsync(cancellationToken);

        return new GetPollResponse(
            Id: poll.Id,
            MessageId: poll.MessageId,
            Question: poll.Question,
            AllowMultipleAnswers: poll.AllowMultipleAnswers,
            ExpiresAt: poll.ExpiresAt,
            IsClosed: poll.IsClosed,
            Options: poll.Options.Select(o => new PollOptionDto(
                Id: o.Id,
                Text: o.Text,
                EmojiUnicode: o.EmojiUnicode,
                VoteCount: o.VoteCount,
                Position: o.Position
            )).ToList(),
            UserVotes: userVotes.Any() ? userVotes : null
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/polls/{pollId}", async (
            long pollId,
            IRequestHandler<GetPollQuery, Result<GetPollResponse>> handler,
            CancellationToken ct) =>
        {
            var query = new GetPollQuery(PollId: pollId);
            return await handler.ExecuteAsync(query, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetPoll")
        .WithTags("Polls");
    }
}
