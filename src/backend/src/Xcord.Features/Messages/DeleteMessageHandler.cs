using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Messages;

public sealed record DeleteMessageRequest(
    long ConversationId,
    long MessageId
);

public sealed class DeleteMessageHandler(
    AppDbContext dbContext,
    IConversationResolver conversationResolver,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    IOutboxWriter outboxWriter,
    ILogger<DeleteMessageHandler> logger)
    : IRequestHandler<DeleteMessageRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteMessageRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Resolve conversation and check membership (no permission required yet)
        var contextResult = await conversationResolver.ResolveAsync(
            request.ConversationId, userId, null, cancellationToken);
        if (contextResult.IsFailure) return contextResult.Error;
        var context = contextResult.Value;

        // For now, we only support Channel conversations
        if (context.Type != ConversationType.Channel)
        {
            return Error.Validation("UNSUPPORTED_CONVERSATION_TYPE", "Only channel conversations are currently supported");
        }

        // Get the message
        var message = await dbContext.Messages
            .FirstOrDefaultAsync(m => m.Id == request.MessageId && m.ConversationId == request.ConversationId, cancellationToken);

        if (message == null)
        {
            return Error.NotFound("MESSAGE_NOT_FOUND", "Message not found");
        }

        // Check if user is the author or has ManageMessages permission
        var isAuthor = message.AuthorId == userId;
        var hasManagePermission = false;

        if (!isAuthor)
        {
            var permissionResult = await roleService.EnsureChannelRole(
                userId,
                context.ChannelId,
                Role.ManageMessages);

            hasManagePermission = permissionResult.IsSuccess;
        }

        if (!isAuthor && !hasManagePermission)
        {
            return Error.Forbidden("CANNOT_DELETE_MESSAGE", "You can only delete your own messages unless you have ManageMessages permission");
        }

        // Soft delete the message
        message.DeletedAt = DateTimeOffset.UtcNow;

        // Write outbox event
        await outboxWriter.WriteAsync(dbContext, "Message.Deleted", new
        {
            messageId = message.Id,
            conversationId = request.ConversationId
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} deleted message {MessageId} in conversation {ConversationId}",
            userId, message.Id, request.ConversationId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/conversations/{conversationId}/messages/{messageId}", async (
            long conversationId,
            long messageId,
            [FromServices] DeleteMessageHandler handler,
            CancellationToken ct) =>
        {
            var request = new DeleteMessageRequest(
                ConversationId: conversationId,
                MessageId: messageId
            );

            return await handler.ExecuteAsync(request, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteMessage")
        .WithTags("Messages");
    }
}
