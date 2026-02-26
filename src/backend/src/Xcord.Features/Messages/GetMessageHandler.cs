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

public sealed record GetMessageRequest(
    long ConversationId,
    long MessageId
);

public sealed record GetMessageResponse(
    long Id,
    long ConversationId,
    long? AuthorId,
    string? AuthorUsername,
    string? AuthorAvatarUrl,
    MessageType Type,
    string Content,
    string? Metadata,
    long? ReplyToId,
    bool IsPinned,
    DateTimeOffset? EditedAt,
    DateTimeOffset CreatedAt
);

public sealed class GetMessageHandler(
    AppDbContext dbContext,
    IConversationResolver conversationResolver,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetMessageRequest, Result<GetMessageResponse>>
{
    public async Task<Result<GetMessageResponse>> Handle(GetMessageRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Resolve conversation and check permissions
        var contextResult = await conversationResolver.ResolveAsync(
            request.ConversationId, userId, Permission.ReadMessageHistory, cancellationToken);
        if (contextResult.IsFailure) return contextResult.Error;

        // For now, we only support Channel conversations
        if (contextResult.Value.Type != ConversationType.Channel)
        {
            return Error.Validation("UNSUPPORTED_CONVERSATION_TYPE", "Only channel conversations are currently supported");
        }

        // Get the message
        var messageData = await dbContext.Messages
            .AsNoTracking()
            .Where(m => m.Id == request.MessageId && m.ConversationId == request.ConversationId)
            .Select(m => new
            {
                Message = m,
                Author = m.Author
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (messageData == null)
        {
            return Error.NotFound("MESSAGE_NOT_FOUND", "Message not found");
        }

        return new GetMessageResponse(
            Id: messageData.Message.Id,
            ConversationId: messageData.Message.ConversationId,
            AuthorId: messageData.Message.AuthorId,
            AuthorUsername: messageData.Author != null ? messageData.Author.Username : null,
            AuthorAvatarUrl: messageData.Author != null ? messageData.Author.AvatarUrl : null,
            Type: messageData.Message.Type,
            Content: messageData.Message.Content,
            Metadata: messageData.Message.Metadata,
            ReplyToId: messageData.Message.ReplyToId,
            IsPinned: messageData.Message.IsPinned,
            EditedAt: messageData.Message.EditedAt,
            CreatedAt: messageData.Message.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/conversations/{conversationId}/messages/{messageId}", async (
            long conversationId,
            long messageId,
            [FromServices] GetMessageHandler handler,
            CancellationToken ct) =>
        {
            var request = new GetMessageRequest(
                ConversationId: conversationId,
                MessageId: messageId
            );

            return await handler.ExecuteAsync(request, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetMessage")
        .WithTags("Messages");
    }
}
