using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Polls;

public sealed record RetractVoteCommand(
    long PollId
);

public sealed record RetractVoteResponse(
    long PollId,
    List<VoteOptionDto> Options
);

public sealed class RetractVoteHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    ILogger<RetractVoteHandler> logger)
    : IRequestHandler<RetractVoteCommand, Result<RetractVoteResponse>>
{
    public async Task<Result<RetractVoteResponse>> Handle(RetractVoteCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get poll with options
        var poll = await dbContext.Polls
            .Include(p => p.Options)
            .Include(p => p.Message)
            .FirstOrDefaultAsync(p => p.Id == request.PollId, cancellationToken);

        if (poll == null)
        {
            return Error.NotFound("POLL_NOT_FOUND", "Poll not found");
        }

        // Begin transaction
        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Get existing votes by this user for this poll
            var existingVotes = await dbContext.PollVotes
                .Where(pv => poll.Options.Select(o => o.Id).Contains(pv.PollOptionId) && pv.UserId == userId)
                .ToListAsync(cancellationToken);

            if (!existingVotes.Any())
            {
                // No votes to retract - this is not an error, just a no-op
                return new RetractVoteResponse(
                    PollId: poll.Id,
                    Options: poll.Options.Select(o => new VoteOptionDto(
                        Id: o.Id,
                        VoteCount: o.VoteCount
                    )).ToList()
                );
            }

            // Remove existing votes (hard delete)
            dbContext.PollVotes.RemoveRange(existingVotes);

            // Decrement vote counts
            foreach (var existingVote in existingVotes)
            {
                var option = poll.Options.First(o => o.Id == existingVote.PollOptionId);
                option.VoteCount--;
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Notify after save
            await notificationService.NotifyConversationAsync(poll.Message.ConversationId, "Poll_Voted", new
            {
                PollId = poll.Id,
                ConversationId = poll.Message.ConversationId,
                Options = poll.Options.Select(o => new
                {
                    Id = o.Id,
                    VoteCount = o.VoteCount
                }).ToList()
            }, cancellationToken);

            logger.LogInformation(
                "User {UserId} retracted vote on poll {PollId}",
                userId, request.PollId);

            return new RetractVoteResponse(
                PollId: poll.Id,
                Options: poll.Options.Select(o => new VoteOptionDto(
                    Id: o.Id,
                    VoteCount: o.VoteCount
                )).ToList()
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
        return app.MapDelete("/api/v1/polls/{pollId}/vote", async (
            long pollId,
            IRequestHandler<RetractVoteCommand, Result<RetractVoteResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new RetractVoteCommand(PollId: pollId);
            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("RetractVote")
        .WithTags("Polls");
    }
}
