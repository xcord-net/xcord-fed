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

public sealed record SendMessageRequest(
    long ConversationId,
    string Content,
    MessageType Type = MessageType.Default,
    long? ReplyToId = null,
    string[]? AttachmentIds = null
);

public sealed record SendMessageResponse(
    long Id,
    long ConversationId,
    long? AuthorId,
    string AuthorUsername,
    string? AuthorAvatarUrl,
    MessageType Type,
    string Content,
    string? Metadata,
    long? ReplyToId,
    bool IsPinned,
    DateTimeOffset? EditedAt,
    DateTimeOffset CreatedAt,
    List<AttachmentDto>? Attachments = null
);

public sealed class SendMessageHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IConversationResolver conversationResolver,
    IRoleService roleService,
    IMessageProcessor messageProcessor,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    IAutomodActionExecutor automodActionExecutor,
    IStorageService storageService,
    ILogger<SendMessageHandler> logger)
    : IRequestHandler<SendMessageRequest, Result<SendMessageResponse>>, IValidatable<SendMessageRequest>
{
    public Error? Validate(SendMessageRequest request)
    {
        var hasAttachments = request.AttachmentIds is { Length: > 0 };
        if (string.IsNullOrWhiteSpace(request.Content) && !hasAttachments)
            return Error.Validation("VALIDATION_FAILED", "Message content is required");

        if (request.Content.Length > 4000)
            return Error.Validation("VALIDATION_FAILED", "Message content must not exceed 4000 characters");

        if (request.Type != MessageType.Default && request.Type != MessageType.PollCreated)
            return Error.Validation("VALIDATION_FAILED", "Invalid message type for user messages");

        return null;
    }

    public async Task<Result<SendMessageResponse>> Handle(SendMessageRequest request, CancellationToken cancellationToken)
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

        long serverId = context.ServerId;
        long channelId = context.ChannelId;

        // If replying, verify the reply target exists and is in the same conversation
        if (request.ReplyToId.HasValue)
        {
            var replyToExists = await dbContext.Messages
                .AsNoTracking()
                .AnyAsync(m => m.Id == request.ReplyToId.Value && m.ConversationId == request.ConversationId, cancellationToken);

            if (!replyToExists)
            {
                return Error.NotFound("REPLY_MESSAGE_NOT_FOUND", "Reply target message not found in this conversation");
            }
        }

        var now = DateTimeOffset.UtcNow;

        // Create message entity
        var messageId = snowflakeGenerator.NextId();
        var message = new Message
        {
            Id = messageId,
            ConversationId = request.ConversationId,
            AuthorId = userId,
            Type = request.Type,
            Content = request.Content,
            ReplyToId = request.ReplyToId,
            IsPinned = false,
            CreatedAt = now
        };

        var hasContent = !string.IsNullOrWhiteSpace(request.Content);

        // Run through processing pipeline (skip automod/mentions for DM conversations and
        // attachment-only messages - channel messages are sanitized inside MessageProcessor)
        var deferredActions = new List<AutomodDeferredAction>();
        if (context.Type != ConversationType.DmChannel && hasContent)
        {
            // Get author's group IDs and bot status for automod
            var authorGroupIds = await dbContext.MemberGroups
                .AsNoTracking()
                .Where(mg => mg.UserId == userId && mg.Group.ServerId == serverId)
                .Select(mg => mg.GroupId)
                .ToListAsync(cancellationToken);

            var author = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            var isBot = author?.IsBot ?? false;

            var processingResult = await messageProcessor.ProcessAsync(message, serverId, channelId, authorGroupIds, isBot).ConfigureAwait(false);
            if (processingResult.IsFailure)
            {
                return processingResult.Error;
            }

            message = processingResult.Value.Message;
            deferredActions = processingResult.Value.DeferredActions;
        }
        else if (hasContent)
        {
            // DM messages skip the processing pipeline (automod/mentions) but still
            // need HTML encoding to prevent stored XSS (V12 fix)
            message.Content = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(message.Content).Trim();
        }

        // Get author info for response (re-fetch if not already fetched above)
        var authorForResponse = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (authorForResponse == null)
        {
            return Error.NotFound("AUTHOR_NOT_FOUND", "Author not found");
        }

        // Begin transaction to persist message and mentions
        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            dbContext.Messages.Add(message);

            // Add mentions
            foreach (var mention in message.Mentions)
            {
                dbContext.Mentions.Add(mention);
            }

            // If this is a thread conversation, update thread metadata
            if (context.Type == ConversationType.Thread)
            {
                var thread = await dbContext.Threads
                    .FirstOrDefaultAsync(t => t.ConversationId == request.ConversationId, cancellationToken);

                if (thread != null)
                {
                    // Update LastActivityAt and increment MessageCount
                    thread.LastActivityAt = DateTimeOffset.UtcNow;
                    thread.MessageCount++;

                    // Auto-join the author if not already a member
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

            // Bulk-increment UnreadCount for all users EXCEPT the author using a single UPDATE
            // statement. ExecuteUpdateAsync performs one SQL UPDATE without loading rows into
            // memory, avoiding per-member tracking overhead.
            await dbContext.ReadStates
                .Where(rs => rs.ConversationId == request.ConversationId && rs.UserId != userId)
                .ExecuteUpdateAsync(s => s.SetProperty(rs => rs.UnreadCount, rs => rs.UnreadCount + 1), cancellationToken);

            // If there are mentions, bulk-increment MentionCount for mentioned users
            var mentionedUserIds = message.Mentions
                .Where(m => m.MentionedUserId.HasValue)
                .Select(m => m.MentionedUserId!.Value)
                .Distinct()
                .ToList();

            if (mentionedUserIds.Any())
            {
                await dbContext.ReadStates
                    .Where(rs => rs.ConversationId == request.ConversationId
                        && mentionedUserIds.Contains(rs.UserId))
                    .ExecuteUpdateAsync(s => s.SetProperty(rs => rs.MentionCount, rs => rs.MentionCount + 1), cancellationToken);
            }

            // Flush message + mentions to DB so FKs are satisfied
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Notify each non-author member of their updated unread count after save.
            var affectedReadStates = await dbContext.ReadStates
                .Where(rs => rs.ConversationId == request.ConversationId && rs.UserId != userId)
                .Select(rs => new { rs.UserId, rs.UnreadCount })
                .ToListAsync(cancellationToken);

            foreach (var rs in affectedReadStates)
            {
                await notificationService.NotifyUserAsync(rs.UserId, "Notify_UnreadUpdated", new
                {
                    userId = rs.UserId,
                    conversationId = request.ConversationId,
                    count = rs.UnreadCount,
                    lastMessageId = message.Id
                }, cancellationToken);
            }

            // Link attachments to this message (must happen after SaveChanges
            // because ExecuteUpdateAsync runs direct SQL that needs the message
            // row to exist for the FK constraint on attachments.MessageId)
            List<AttachmentDto>? attachmentDtos = null;
            if (request.AttachmentIds is { Length: > 0 })
            {
                var parsedIds = request.AttachmentIds
                    .Where(id => long.TryParse(id, out _))
                    .Select(id => long.Parse(id))
                    .ToList();

                if (parsedIds.Count > 0)
                {
                    await dbContext.Attachments
                        .Where(a => parsedIds.Contains(a.Id)
                            && a.IsConfirmed
                            && a.MessageId == null
                            && a.DeletedAt == null)
                        .ExecuteUpdateAsync(s => s.SetProperty(a => a.MessageId, messageId), cancellationToken);

                    // Query linked attachments and generate pre-signed URLs
                    var linkedAttachments = await dbContext.Attachments
                        .AsNoTracking()
                        .Where(a => a.MessageId == messageId && a.DeletedAt == null)
                        .ToListAsync(cancellationToken);

                    if (linkedAttachments.Count > 0)
                    {
                        var urlTasks = linkedAttachments.Select(async a =>
                        {
                            var downloadUrl = await storageService.GenerateDownloadUrlAsync(a.S3Key, TimeSpan.FromHours(1)).ConfigureAwait(false);
                            string? thumbnailUrl = null;
                            if (!string.IsNullOrEmpty(a.ThumbnailS3Key))
                            {
                                thumbnailUrl = await storageService.GenerateDownloadUrlAsync(a.ThumbnailS3Key, TimeSpan.FromHours(1)).ConfigureAwait(false);
                            }
                            return new AttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSize, a.Width, a.Height, downloadUrl, thumbnailUrl);
                        });
                        attachmentDtos = (await Task.WhenAll(urlTasks).ConfigureAwait(false)).ToList();
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Notify conversation of new message after commit - include full message data
            // (including attachments) so the client can render immediately.
            var notifyAttachments = attachmentDtos?.Select(a => (object)new
            {
                id = a.Id,
                fileName = a.FileName,
                contentType = a.ContentType,
                fileSize = a.FileSize,
                width = a.Width,
                height = a.Height,
                downloadUrl = a.DownloadUrl,
                thumbnailUrl = a.ThumbnailUrl
            }).ToList();

            await notificationService.NotifyConversationAsync(request.ConversationId, "Chat_MessageCreated",
                MessageEventPayloads.ForCreated(message, authorForResponse.Username, authorForResponse.AvatarUrl,
                    attachments: notifyAttachments), cancellationToken);

            logger.LogInformation(
                "User {UserId} sent message {MessageId} in conversation {ConversationId}",
                userId, messageId, request.ConversationId);

            // Execute deferred automod actions after commit
            if (deferredActions.Any())
            {
                await automodActionExecutor.ExecuteDeferredActionsAsync(messageId, serverId, channelId, userId, deferredActions, cancellationToken).ConfigureAwait(false);
            }

            return new SendMessageResponse(
                Id: message.Id,
                ConversationId: message.ConversationId,
                AuthorId: message.AuthorId,
                AuthorUsername: authorForResponse.Username,
                AuthorAvatarUrl: authorForResponse.AvatarUrl,
                Type: message.Type,
                Content: message.Content,
                Metadata: message.Metadata,
                ReplyToId: message.ReplyToId,
                IsPinned: message.IsPinned,
                EditedAt: message.EditedAt,
                CreatedAt: message.CreatedAt,
                Attachments: attachmentDtos
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
        return app.MapPost("/api/v1/conversations/{conversationId}/messages", async (
            long conversationId,
            [FromBody] SendMessageBodyRequest bodyRequest,
            [FromServices] SendMessageHandler handler,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var request = new SendMessageRequest(
                ConversationId: conversationId,
                Content: bodyRequest.Content,
                Type: bodyRequest.Type ?? MessageType.Default,
                ReplyToId: bodyRequest.ReplyToId,
                AttachmentIds: bodyRequest.AttachmentIds
            );

            // Run validation (mirrors ExecuteAsync behaviour)
            var validationError = handler.Validate(request);
            if (validationError is not null)
                return Results.Problem(
                    statusCode: validationError.StatusCode,
                    title: validationError.Code,
                    detail: validationError.Message);

            var result = await handler.Handle(request, ct).ConfigureAwait(false);
            return result.Match(
                success => Results.Created(
                    $"/api/v1/conversations/{conversationId}/messages/{success.Id}", success),
                err =>
                {
                    if (err.StatusCode == 429 && err.Code == "SLOWMODE_RATE_LIMITED"
                        && int.TryParse(err.Message, out var retryAfter))
                    {
                        httpContext.Response.Headers["Retry-After"] = retryAfter.ToString();
                        return Results.Problem(
                            statusCode: 429,
                            title: err.Code,
                            detail: $"Slowmode is enabled. Please wait {retryAfter} second(s) before sending another message.");
                    }
                    return Results.Problem(statusCode: err.StatusCode, title: err.Code, detail: err.Message);
                });
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("SendMessage")
        .WithTags("Messages");
    }
}

public sealed record SendMessageBodyRequest(
    string Content,
    MessageType? Type = null,
    long? ReplyToId = null,
    string[]? AttachmentIds = null
);
