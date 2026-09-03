using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Messages;

public sealed record AddReactionCommand(long ConversationId, long MessageId, string Emoji);
public sealed record ReactionResponse(long MessageId, long UserId, string Emoji, DateTimeOffset CreatedAt);

public sealed class AddReactionHandler(
    AppDbContext dbContext, ICurrentUserService currentUserService,
    INotificationService notificationService)
    : IRequestHandler<AddReactionCommand, Result<ReactionResponse>>
{
    public async Task<Result<ReactionResponse>> Handle(AddReactionCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var message = await dbContext.Messages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MessageId && m.ConversationId == request.ConversationId, ct);
        if (message == null) return Error.NotFound("MESSAGE_NOT_FOUND", "Message not found");

        var existing = await dbContext.Reactions
            .FirstOrDefaultAsync(r => r.MessageId == request.MessageId && r.UserId == userId && r.Emoji == request.Emoji, ct);

        if (existing != null)
            return new ReactionResponse(existing.MessageId, existing.UserId, existing.Emoji, existing.CreatedAt);

        var now = DateTimeOffset.UtcNow;
        var reaction = new Reaction
        {
            MessageId = request.MessageId,
            UserId = userId,
            Emoji = request.Emoji,
            CreatedAt = now
        };
        dbContext.Reactions.Add(reaction);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        await ReactionBroadcast.PublishAsync(
            dbContext, notificationService, request.ConversationId, request.MessageId, ct)
            .ConfigureAwait(false);

        return new ReactionResponse(reaction.MessageId, userId, reaction.Emoji, now);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/conversations/{conversationId}/messages/{messageId}/reactions/{emoji}", async (
            long conversationId, long messageId, string emoji,
            IRequestHandler<AddReactionCommand, Result<ReactionResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new AddReactionCommand(conversationId, messageId, Uri.UnescapeDataString(emoji)), ct))
        .RequireAuthorization(Policies.User)
        .WithName("AddReaction").WithTags("Reactions");
}
