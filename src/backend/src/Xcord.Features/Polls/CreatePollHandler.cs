using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Polls;

public sealed record CreatePollCommand(
    long ConversationId,
    string Question,
    List<CreatePollOptionDto> Options,
    bool AllowMultipleAnswers = false,
    DateTimeOffset? ExpiresAt = null
);

public sealed record CreatePollOptionDto(
    string Text,
    string? EmojiUnicode = null
);

public sealed record CreatePollResponse(
    long PollId,
    long MessageId,
    long ConversationId,
    string Question,
    bool AllowMultipleAnswers,
    DateTimeOffset? ExpiresAt,
    List<PollOptionDto> Options,
    DateTimeOffset CreatedAt
);

public sealed record PollOptionDto(
    long Id,
    string Text,
    string? EmojiUnicode,
    int VoteCount,
    int Position
);

public sealed class CreatePollHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IConversationResolver conversationResolver,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    ILogger<CreatePollHandler> logger)
    : IRequestHandler<CreatePollCommand, Result<CreatePollResponse>>, IValidatable<CreatePollCommand>
{
    public Error? Validate(CreatePollCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Error.Validation("VALIDATION_ERROR", "Poll question is required");
        }

        if (request.Question.Length > 300)
        {
            return Error.Validation("VALIDATION_ERROR", "Poll question must not exceed 300 characters");
        }

        if (request.Options == null || request.Options.Count == 0)
        {
            return Error.Validation("VALIDATION_ERROR", "At least one poll option is required");
        }

        if (request.Options.Count < 2)
        {
            return Error.Validation("VALIDATION_ERROR", "Poll must have at least 2 options");
        }

        if (request.Options.Count > 10)
        {
            return Error.Validation("VALIDATION_ERROR", "Poll must have at most 10 options");
        }

        foreach (var option in request.Options)
        {
            if (string.IsNullOrWhiteSpace(option.Text))
            {
                return Error.Validation("VALIDATION_ERROR", "Option text is required");
            }

            if (option.Text.Length > 100)
            {
                return Error.Validation("VALIDATION_ERROR", "Option text must not exceed 100 characters");
            }

            if (option.EmojiUnicode != null && option.EmojiUnicode.Length > 32)
            {
                return Error.Validation("VALIDATION_ERROR", "Emoji unicode must not exceed 32 characters");
            }
        }

        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            return Error.Validation("VALIDATION_ERROR", "Expiration date must be in the future");
        }

        return null;
    }

    public async Task<Result<CreatePollResponse>> Handle(CreatePollCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Resolve conversation (permissions checked below based on type)
        var contextResult = await conversationResolver.ResolveAsync(
            request.ConversationId, userId, null, cancellationToken);
        if (contextResult.IsFailure) return contextResult.Error;
        var context = contextResult.Value;

        // Check permissions based on conversation type
        if (context.Type == ConversationType.Channel)
        {
            var permissionResult = await roleService.EnsureChannelRole(
                userId,
                context.ChannelId,
                Role.SendMessages);

            if (permissionResult.IsFailure)
            {
                return permissionResult.Error;
            }
        }
        else if (context.Type == ConversationType.Thread)
        {
            // Check SendMessagesInThreads permission (locked thread check already done by resolver)
            var permissionResult = await roleService.EnsureChannelRole(
                userId,
                context.ChannelId,
                Role.SendMessagesInThreads);

            if (permissionResult.IsFailure)
            {
                return permissionResult.Error;
            }
        }
        else if (context.Type != ConversationType.DmChannel)
        {
            return Error.Validation("UNSUPPORTED_CONVERSATION_TYPE", "Unsupported conversation type");
        }

        var now = DateTimeOffset.UtcNow;

        // Create message entity
        var messageId = snowflakeGenerator.NextId();
        var message = new Message
        {
            Id = messageId,
            ConversationId = request.ConversationId,
            AuthorId = userId,
            Type = MessageType.PollCreated,
            Content = request.Question,
            CreatedAt = now
        };

        // Create poll entity
        var pollId = snowflakeGenerator.NextId();
        var poll = new Poll
        {
            Id = pollId,
            MessageId = messageId,
            Question = request.Question,
            AllowMultipleAnswers = request.AllowMultipleAnswers,
            ExpiresAt = request.ExpiresAt,
            IsClosed = false
        };

        // Create poll options
        var pollOptions = new List<PollOption>();
        for (int i = 0; i < request.Options.Count; i++)
        {
            var optionDto = request.Options[i];
            var optionId = snowflakeGenerator.NextId();
            var pollOption = new PollOption
            {
                Id = optionId,
                PollId = pollId,
                Text = optionDto.Text,
                EmojiUnicode = optionDto.EmojiUnicode,
                VoteCount = 0,
                Position = i
            };
            pollOptions.Add(pollOption);
        }

        // Begin transaction
        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            dbContext.Messages.Add(message);
            dbContext.Polls.Add(poll);
            dbContext.PollOptions.AddRange(pollOptions);

            // If this is a thread conversation, update thread metadata
            if (context.Type == ConversationType.Thread)
            {
                var thread = await dbContext.Threads
                    .FirstOrDefaultAsync(t => t.ConversationId == request.ConversationId, cancellationToken);

                if (thread != null)
                {
                    thread.LastActivityAt = DateTimeOffset.UtcNow;
                    thread.MessageCount++;

                    var isThreadMember = await dbContext.ThreadMembers
                        .AsNoTracking()
                        .AnyAsync(tm => tm.UserId == userId && tm.ThreadId == thread.Id, cancellationToken);

                    if (!isThreadMember)
                    {
                        var threadMember = new ThreadMember
                        {
                            UserId = userId,
                            ThreadId = thread.Id,
                            JoinedAt = DateTimeOffset.UtcNow
                        };
                        dbContext.ThreadMembers.Add(threadMember);
                    }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Notify after save
            await notificationService.NotifyConversationAsync(message.ConversationId, "Poll_Created", new
            {
                PollId = poll.Id,
                MessageId = message.Id,
                ConversationId = message.ConversationId,
                AuthorId = message.AuthorId,
                Question = poll.Question,
                AllowMultipleAnswers = poll.AllowMultipleAnswers,
                ExpiresAt = poll.ExpiresAt,
                Options = pollOptions.Select(o => new
                {
                    Id = o.Id,
                    Text = o.Text,
                    EmojiUnicode = o.EmojiUnicode,
                    VoteCount = o.VoteCount,
                    Position = o.Position
                }).ToList()
            });

            logger.LogInformation(
                "User {UserId} created poll {PollId} in conversation {ConversationId}",
                userId, pollId, request.ConversationId);

            return new CreatePollResponse(
                PollId: pollId,
                MessageId: messageId,
                ConversationId: request.ConversationId,
                Question: poll.Question,
                AllowMultipleAnswers: poll.AllowMultipleAnswers,
                ExpiresAt: poll.ExpiresAt,
                Options: pollOptions.Select(o => new PollOptionDto(
                    Id: o.Id,
                    Text: o.Text,
                    EmojiUnicode: o.EmojiUnicode,
                    VoteCount: o.VoteCount,
                    Position: o.Position
                )).ToList(),
                CreatedAt: now
            );
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/conversations/{conversationId}/polls", async (
            long conversationId,
            CreatePollRequest request,
            IRequestHandler<CreatePollCommand, Result<CreatePollResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new CreatePollCommand(
                ConversationId: conversationId,
                Question: request.Question,
                Options: request.Options,
                AllowMultipleAnswers: request.AllowMultipleAnswers,
                ExpiresAt: request.ExpiresAt
            );

            return await handler.ExecuteAsync(command, ct,
                onSuccess: poll => Results.Created($"/api/v1/conversations/{conversationId}/polls/{poll.PollId}", poll));
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreatePoll")
        .WithTags("Polls");
    }
}

public sealed record CreatePollRequest(
    string Question,
    List<CreatePollOptionDto> Options,
    bool AllowMultipleAnswers = false,
    DateTimeOffset? ExpiresAt = null
);
