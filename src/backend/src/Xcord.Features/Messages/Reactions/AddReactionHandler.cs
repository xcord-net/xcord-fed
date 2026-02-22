using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Messages.Reactions;

public sealed record AddReactionCommand(long ConversationId, long MessageId, string Emoji);
public sealed record ReactionResponse(long MessageId, long UserId, string Emoji, bool IsSuper, DateTimeOffset CreatedAt);

public sealed class AddReactionHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<AddReactionCommand, Result<ReactionResponse>>
{
    public async Task<Result<ReactionResponse>> Handle(AddReactionCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var message = await dbContext.Messages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MessageId && m.ConversationId == request.ConversationId, ct);
        if (message == null) return Error.NotFound("MESSAGE_NOT_FOUND", "Message not found");

        var existing = await dbContext.Reactions
            .FirstOrDefaultAsync(r => r.MessageId == request.MessageId && r.UserId == userId && r.Emoji == request.Emoji, ct);

        if (existing != null)
            return new ReactionResponse(existing.MessageId, existing.UserId, existing.Emoji, existing.IsSuper, existing.CreatedAt);

        var now = DateTimeOffset.UtcNow;
        var reaction = new Reaction
        {
            MessageId = request.MessageId,
            UserId = userId,
            Emoji = request.Emoji,
            IsSuper = false,
            CreatedAt = now
        };
        dbContext.Reactions.Add(reaction);
        await dbContext.SaveChangesAsync(ct);

        return new ReactionResponse(reaction.MessageId, userId, reaction.Emoji, false, now);
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
