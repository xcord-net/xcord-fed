using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Messages;

public sealed record EditMessageRequest(
    long ConversationId,
    long MessageId,
    string Content
);

public sealed record EditMessageResponse(
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

public sealed class EditMessageHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IConversationResolver conversationResolver,
    IRoleService roleService,
    IMessageProcessor messageProcessor,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    IAutomodActionExecutor automodActionExecutor,
    ILogger<EditMessageHandler> logger)
    : IRequestHandler<EditMessageRequest, Result<EditMessageResponse>>, IValidatable<EditMessageRequest>
{
    public Error? Validate(EditMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return Error.Validation("VALIDATION_FAILED", "Message content is required");

        if (request.Content.Length > 4000)
            return Error.Validation("VALIDATION_FAILED", "Message content must not exceed 4000 characters");

        return null;
    }

    public async Task<Result<EditMessageResponse>> Handle(EditMessageRequest request, CancellationToken cancellationToken)
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

        // Get the message (with tracking for update)
        var message = await dbContext.Messages
            .Include(m => m.Mentions)
            .Include(m => m.Author)
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
            return Error.Forbidden("CANNOT_EDIT_MESSAGE", "You can only edit your own messages unless you have ManageMessages permission");
        }

        // Capture edit history BEFORE updating content
        var messageEditId = snowflakeGenerator.NextId();
        var messageEdit = new MessageEdit
        {
            Id = messageEditId,
            MessageId = message.Id,
            PreviousContent = message.Content,
            EditedAt = DateTimeOffset.UtcNow
        };

        dbContext.MessageEdits.Add(messageEdit);

        // Update content and re-run processing pipeline
        message.Content = request.Content;

        // Remove old mentions
        dbContext.Mentions.RemoveRange(message.Mentions);

        // Get author group IDs for automod processing
        var authorGroupIds = await dbContext.MemberGroups
            .AsNoTracking()
            .Where(mg => mg.UserId == userId && mg.ServerId == context.ServerId)
            .Select(mg => mg.GroupId)
            .ToListAsync(cancellationToken);

        // Check if author is a bot
        var isBot = await dbContext.BotTokens
            .AsNoTracking()
            .AnyAsync(bt => bt.UserId == userId, cancellationToken);

        // Re-process message
        var processingResult = await messageProcessor.ProcessAsync(message, context.ServerId, context.ChannelId, authorGroupIds, isBot).ConfigureAwait(false);
        if (processingResult.IsFailure)
        {
            return processingResult.Error;
        }

        // Set EditedAt timestamp
        message.EditedAt = DateTimeOffset.UtcNow;

        // Add new mentions
        foreach (var mention in message.Mentions)
        {
            dbContext.Mentions.Add(mention);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Notify conversation after save - include full message data so clients can update their
        // message store immediately without a separate API fetch.
        await notificationService.NotifyConversationAsync(request.ConversationId, "Chat_MessageUpdated",
            MessageEventPayloads.ForCreated(message, message.Author?.Username, message.Author?.AvatarUrl, message.EditedAt), cancellationToken);

        // Execute deferred automod actions after save
        var deferredActions = processingResult.Value.DeferredActions;
        if (deferredActions.Any())
        {
            await automodActionExecutor.ExecuteDeferredActionsAsync(message.Id, context.ServerId, context.ChannelId, userId, deferredActions, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation(
            "User {UserId} edited message {MessageId} in conversation {ConversationId}",
            userId, message.Id, request.ConversationId);

        return new EditMessageResponse(
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

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/conversations/{conversationId}/messages/{messageId}", async (
            long conversationId,
            long messageId,
            [FromBody] EditMessageBodyRequest bodyRequest,
            [FromServices] EditMessageHandler handler,
            CancellationToken ct) =>
        {
            var request = new EditMessageRequest(
                ConversationId: conversationId,
                MessageId: messageId,
                Content: bodyRequest.Content
            );

            return await handler.ExecuteAsync(request, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("EditMessage")
        .WithTags("Messages");
    }
}

public sealed record EditMessageBodyRequest(
    string Content
);
