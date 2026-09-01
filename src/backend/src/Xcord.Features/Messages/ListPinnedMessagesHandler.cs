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

public sealed record ListPinnedMessagesRequest(
    long ConversationId
);

public sealed record ListPinnedMessagesResponse(
    List<MessageDto> Messages
);

public sealed class ListPinnedMessagesHandler(
    AppDbContext dbContext,
    IConversationResolver conversationResolver,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListPinnedMessagesRequest, Result<ListPinnedMessagesResponse>>
{
    public async Task<Result<ListPinnedMessagesResponse>> Handle(ListPinnedMessagesRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify the user has ReadMessageHistory permission on this conversation
        var contextResult = await conversationResolver.ResolveAsync(
            request.ConversationId, userId, Role.ReadMessageHistory, cancellationToken);
        if (contextResult.IsFailure) return contextResult.Error;

        // Query pinned messages for the conversation, ordered by PinnedAt descending
        var messageDtos = await MessageDtos.FromQueryAsync(
            dbContext,
            dbContext.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == request.ConversationId && m.IsPinned)
                .OrderByDescending(m => m.PinnedAt),
            cancellationToken);

        return new ListPinnedMessagesResponse(Messages: messageDtos);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/conversations/{conversationId:long}/pins", async (
            long conversationId,
            [FromServices] ListPinnedMessagesHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new ListPinnedMessagesRequest(conversationId), ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListPinnedMessages")
        .WithTags("Messages");
}
