using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.ReadState;

public sealed record MarkAsReadBodyRequest(long MessageId);

public sealed record MarkAsReadRequest(
    long ConversationId,
    long MessageId
);

public sealed record MarkAsReadResponse(
    long ConversationId,
    long LastReadMessageId,
    int UnreadCount,
    int MentionCount
);

public sealed class MarkAsReadHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    INotificationService notificationService) : IRequestHandler<MarkAsReadRequest, Result<MarkAsReadResponse>>, IValidatable<MarkAsReadRequest>
{
    public Error? Validate(MarkAsReadRequest request)
    {
        if (request.ConversationId <= 0)
            return Error.Validation("VALIDATION_FAILED", "ConversationId must be greater than 0");

        if (request.MessageId <= 0)
            return Error.Validation("VALIDATION_FAILED", "MessageId must be greater than 0");

        return null;
    }

    public async Task<Result<MarkAsReadResponse>> Handle(MarkAsReadRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify conversation exists
        var conversationExists = await dbContext.Conversations
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.ConversationId, cancellationToken);

        if (!conversationExists)
        {
            return Error.NotFound("CONVERSATION_NOT_FOUND", "Conversation not found");
        }

        // Verify message exists and belongs to this conversation
        var messageExists = await dbContext.Messages
            .AsNoTracking()
            .AnyAsync(m => m.Id == request.MessageId && m.ConversationId == request.ConversationId, cancellationToken);

        if (!messageExists)
        {
            return Error.NotFound("MESSAGE_NOT_FOUND", "Message not found in this conversation");
        }

        // Begin transaction
        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Upsert ReadState
            var readState = await dbContext.ReadStates
                .FirstOrDefaultAsync(rs => rs.UserId == userId && rs.ConversationId == request.ConversationId, cancellationToken);

            if (readState == null)
            {
                // Create new read state
                readState = new Entities.ReadState
                {
                    UserId = userId,
                    ConversationId = request.ConversationId,
                    LastReadMessageId = request.MessageId,
                    UnreadCount = 0,
                    MentionCount = 0
                };

                dbContext.ReadStates.Add(readState);
            }
            else
            {
                // Update existing read state
                readState.LastReadMessageId = request.MessageId;
                readState.UnreadCount = 0;
                readState.MentionCount = 0;
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Notify the conversation group directly after save
            await notificationService.NotifyConversationAsync(request.ConversationId, "ReadState_Updated", new
            {
                UserId = userId,
                ConversationId = request.ConversationId,
                LastReadMessageId = request.MessageId
            }, cancellationToken);

            return new MarkAsReadResponse(
                ConversationId: readState.ConversationId,
                LastReadMessageId: readState.LastReadMessageId ?? 0,
                UnreadCount: readState.UnreadCount,
                MentionCount: readState.MentionCount
            );
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/conversations/{conversationId}/read-state", async (
            long conversationId,
            [FromBody] MarkAsReadBodyRequest request,
            [FromServices] MarkAsReadHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new MarkAsReadRequest(conversationId, request.MessageId), ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("MarkAsRead")
        .WithTags("ReadState");
}
