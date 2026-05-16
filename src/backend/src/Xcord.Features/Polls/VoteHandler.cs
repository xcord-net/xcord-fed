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

public sealed record VoteCommand(
    long PollId,
    List<long> OptionIds
);

public sealed record VoteResponse(
    long PollId,
    List<VoteOptionDto> Options
);

public sealed record VoteOptionDto(
    long Id,
    int VoteCount
);

public sealed class VoteHandler(
    AppDbContext dbContext,
    IConversationResolver conversationResolver,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    ILogger<VoteHandler> logger)
    : IRequestHandler<VoteCommand, Result<VoteResponse>>, IValidatable<VoteCommand>
{
    public Error? Validate(VoteCommand request)
    {
        if (request.OptionIds == null || request.OptionIds.Count == 0)
        {
            return Error.Validation("VALIDATION_ERROR", "At least one option ID is required");
        }

        return null;
    }

    public async Task<Result<VoteResponse>> Handle(VoteCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get poll with options
        var poll = await dbContext.Polls
            .Include(p => p.Options)
            .Include(p => p.Message)
                .ThenInclude(m => m.Conversation)
            .FirstOrDefaultAsync(p => p.Id == request.PollId, cancellationToken);

        if (poll == null)
        {
            return Error.NotFound("POLL_NOT_FOUND", "Poll not found");
        }

        // Check if poll is closed or expired
        if (poll.IsClosed)
        {
            return Error.Validation("POLL_CLOSED", "Poll is closed");
        }

        if (poll.ExpiresAt.HasValue && poll.ExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            return Error.Validation("POLL_EXPIRED", "Poll has expired");
        }

        // Verify user is a member of the conversation
        var conversation = poll.Message.Conversation;

        // Resolve conversation to verify membership
        var contextResult = await conversationResolver.ResolveAsync(
            conversation.Id, userId, null, cancellationToken);
        if (contextResult.IsFailure) return contextResult.Error;

        // Verify all option IDs belong to this poll
        var validOptionIds = poll.Options.Select(o => o.Id).ToHashSet();
        if (request.OptionIds.Any(id => !validOptionIds.Contains(id)))
        {
            return Error.Validation("INVALID_OPTION", "One or more option IDs are invalid");
        }

        // If single-answer poll, verify only one option selected
        if (!poll.AllowMultipleAnswers && request.OptionIds.Count > 1)
        {
            return Error.Validation("MULTIPLE_ANSWERS_NOT_ALLOWED", "This poll does not allow multiple answers");
        }

        // Begin transaction
        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Get existing votes by this user for this poll
            var existingVotes = await dbContext.PollVotes
                .Where(pv => poll.Options.Select(o => o.Id).Contains(pv.PollOptionId) && pv.UserId == userId)
                .ToListAsync(cancellationToken);

            // Remove existing votes (hard delete)
            if (existingVotes.Any())
            {
                dbContext.PollVotes.RemoveRange(existingVotes);

                // Decrement vote counts for previously voted options
                foreach (var existingVote in existingVotes)
                {
                    var option = poll.Options.First(o => o.Id == existingVote.PollOptionId);
                    option.VoteCount--;
                }
            }

            // Add new votes
            var now = DateTimeOffset.UtcNow;
            var newVotes = new List<PollVote>();

            foreach (var optionId in request.OptionIds)
            {
                var vote = new PollVote
                {
                    PollOptionId = optionId,
                    UserId = userId,
                    CreatedAt = now
                };
                newVotes.Add(vote);

                // Increment vote count
                var option = poll.Options.First(o => o.Id == optionId);
                option.VoteCount++;
            }

            dbContext.PollVotes.AddRange(newVotes);

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Notify after save
            await notificationService.NotifyConversationAsync(conversation.Id, "Poll_Voted", new
            {
                PollId = poll.Id,
                ConversationId = conversation.Id,
                Options = poll.Options.Select(o => new
                {
                    Id = o.Id,
                    VoteCount = o.VoteCount
                }).ToList()
            }, cancellationToken);

            logger.LogInformation(
                "User {UserId} voted on poll {PollId}",
                userId, request.PollId);

            return new VoteResponse(
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
        return app.MapPut("/api/v1/polls/{pollId}/vote", async (
            long pollId,
            VoteRequest request,
            IRequestHandler<VoteCommand, Result<VoteResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new VoteCommand(
                PollId: pollId,
                OptionIds: request.OptionIds
            );

            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("Vote")
        .WithTags("Polls");
    }
}

public sealed record VoteRequest(
    List<long> OptionIds
);
