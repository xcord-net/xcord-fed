using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Messages;

public sealed record UnpinMessageRequest(
    long ConversationId,
    long MessageId
);

public sealed class UnpinMessageHandler(
    AppDbContext dbContext,
    IConversationResolver conversationResolver,
    ICurrentUserService currentUserService,
    IOutboxWriter outboxWriter)
    : IRequestHandler<UnpinMessageRequest, Result<MessageDto>>
{
    public async Task<Result<MessageDto>> Handle(UnpinMessageRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify the user has ManageMessages permission on this conversation
        var contextResult = await conversationResolver.ResolveAsync(
            request.ConversationId, userId, Role.ManageMessages, cancellationToken);
        if (contextResult.IsFailure) return contextResult.Error;

        // Find the message and verify it belongs to the conversation and is pinned
        var message = await dbContext.Messages
            .Include(m => m.Author)
            .FirstOrDefaultAsync(
                m => m.Id == request.MessageId && m.ConversationId == request.ConversationId,
                cancellationToken);

        if (message == null)
        {
            return Error.NotFound("MESSAGE_NOT_FOUND", "Message not found");
        }

        if (!message.IsPinned)
        {
            return Error.Validation("MESSAGE_NOT_PINNED", "Message is not pinned");
        }

        // Unpin the message
        message.IsPinned = false;
        message.PinnedAt = null;

        // Write outbox event
        await outboxWriter.WriteAsync(dbContext, "Chat.MessageUpdated", new
        {
            conversationId = request.ConversationId,
            messageId = request.MessageId
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new MessageDto(
            Id: message.Id,
            ConversationId: message.ConversationId,
            AuthorId: message.AuthorId,
            AuthorUsername: message.Author != null ? message.Author.Username : null,
            AuthorAvatarUrl: message.Author != null ? message.Author.AvatarUrl : null,
            Type: message.Type,
            Content: message.Content,
            Metadata: message.Metadata,
            ReplyToId: message.ReplyToId,
            IsPinned: message.IsPinned,
            EditedAt: message.EditedAt,
            CreatedAt: message.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/conversations/{conversationId:long}/messages/{messageId:long}/pin", async (
            long conversationId,
            long messageId,
            [FromServices] UnpinMessageHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new UnpinMessageRequest(conversationId, messageId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UnpinMessage")
        .WithTags("Messages");
}
