using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Messages;

public sealed record BulkDeleteMessagesRequest(
    long ConversationId,
    List<long> MessageIds
);

public sealed class BulkDeleteMessagesHandler(
    AppDbContext dbContext,
    IConversationResolver conversationResolver,
    IHttpContextAccessor httpContextAccessor,
    IOutboxWriter outboxWriter,
    ILogger<BulkDeleteMessagesHandler> logger)
    : IRequestHandler<BulkDeleteMessagesRequest, Result<bool>>, IValidatable<BulkDeleteMessagesRequest>
{
    public Error? Validate(BulkDeleteMessagesRequest request)
    {
        if (request.MessageIds == null || request.MessageIds.Count == 0)
            return Error.Validation("VALIDATION_FAILED", "At least one message ID is required");

        if (request.MessageIds.Count > 100)
            return Error.Validation("VALIDATION_FAILED", "Cannot delete more than 100 messages at once");

        return null;
    }

    public async Task<Result<bool>> Handle(BulkDeleteMessagesRequest request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Resolve conversation and check ManageMessages permission
        var contextResult = await conversationResolver.ResolveAsync(
            request.ConversationId, userId, Permission.ManageMessages, cancellationToken);
        if (contextResult.IsFailure) return contextResult.Error;

        // For now, we only support Channel conversations
        if (contextResult.Value.Type != ConversationType.Channel)
        {
            return Error.Validation("UNSUPPORTED_CONVERSATION_TYPE", "Only channel conversations are currently supported");
        }

        // Get all messages to delete
        var messages = await dbContext.Messages
            .Where(m => request.MessageIds.Contains(m.Id) && m.ConversationId == request.ConversationId)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return Error.NotFound("NO_MESSAGES_FOUND", "No messages found to delete");
        }

        // Soft delete all messages in a single transaction
        var now = DateTimeOffset.UtcNow;
        foreach (var message in messages)
        {
            message.DeletedAt = now;
        }

        // Write outbox event
        await outboxWriter.WriteAsync(dbContext, "Message.BulkDeleted", new
        {
            messageIds = messages.Select(m => m.Id).ToArray(),
            conversationId = request.ConversationId
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} bulk deleted {Count} messages in conversation {ConversationId}",
            userId, messages.Count, request.ConversationId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/conversations/{conversationId}/messages/bulk-delete", async (
            long conversationId,
            [FromBody] BulkDeleteMessagesBodyRequest bodyRequest,
            [FromServices] BulkDeleteMessagesHandler handler,
            CancellationToken ct) =>
        {
            var request = new BulkDeleteMessagesRequest(
                ConversationId: conversationId,
                MessageIds: bodyRequest.MessageIds
            );

            return await handler.ExecuteAsync(request, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("BulkDeleteMessages")
        .WithTags("Messages");
    }
}

public sealed record BulkDeleteMessagesBodyRequest(
    List<long> MessageIds
);
