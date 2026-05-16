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

public sealed record GetEditHistoryRequest(
    long ConversationId,
    long MessageId
);

public sealed record GetEditHistoryResponse(
    List<MessageEditDto> Edits
);

public sealed record MessageEditDto(
    long Id,
    long MessageId,
    string PreviousContent,
    DateTimeOffset EditedAt
);

public sealed class GetEditHistoryHandler(
    AppDbContext dbContext,
    IRoleService roleService,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetEditHistoryRequest, Result<GetEditHistoryResponse>>
{
    public async Task<Result<GetEditHistoryResponse>> Handle(GetEditHistoryRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get conversation and resolve server ID
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);

        if (conversation == null)
        {
            return Error.NotFound("CONVERSATION_NOT_FOUND", "Conversation not found");
        }

        // For now, we only support Channel conversations
        if (conversation.Type != ConversationType.Channel)
        {
            return Error.Validation("UNSUPPORTED_CONVERSATION_TYPE", "Only channel conversations are currently supported");
        }

        // Get the channel to resolve server ID
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == request.ConversationId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        // Verify user is a member of the server
        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == channel.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_MEMBER", "User is not a member of this server");
        }

        // Check ReadMessageHistory permission
        var permissionResult = await roleService.EnsureChannelRole(
            userId,
            channel.Id,
            Role.ReadMessageHistory);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Verify the message exists in the conversation
        var messageExists = await dbContext.Messages
            .AsNoTracking()
            .AnyAsync(m => m.Id == request.MessageId && m.ConversationId == request.ConversationId, cancellationToken);

        if (!messageExists)
        {
            return Error.NotFound("MESSAGE_NOT_FOUND", "Message not found in this conversation");
        }

        // Get edit history ordered by most recent first
        var edits = await dbContext.MessageEdits
            .AsNoTracking()
            .Where(e => e.MessageId == request.MessageId)
            .OrderByDescending(e => e.EditedAt)
            .ToListAsync(cancellationToken);

        var editDtos = edits.Select(e => new MessageEditDto(
            e.Id,
            e.MessageId,
            e.PreviousContent,
            e.EditedAt
        )).ToList();

        return new GetEditHistoryResponse(editDtos);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/conversations/{conversationId}/messages/{messageId}/edits", async (
            long conversationId,
            long messageId,
            [FromServices] GetEditHistoryHandler handler,
            CancellationToken ct) =>
        {
            var request = new GetEditHistoryRequest(
                ConversationId: conversationId,
                MessageId: messageId
            );

            return await handler.ExecuteAsync(request, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetEditHistory")
        .WithTags("Messages", "Edit History");
    }
}
