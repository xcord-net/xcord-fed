using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Messages.Reactions;

public sealed record RemoveReactionCommand(long ConversationId, long MessageId, string Emoji);

public sealed class RemoveReactionHandler(
    AppDbContext dbContext, ICurrentUserService currentUserService)
    : IRequestHandler<RemoveReactionCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveReactionCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var reaction = await dbContext.Reactions
            .FirstOrDefaultAsync(r => r.MessageId == request.MessageId && r.UserId == userId && r.Emoji == request.Emoji, ct);

        if (reaction == null)
            return Error.NotFound("REACTION_NOT_FOUND", "Reaction not found");

        dbContext.Reactions.Remove(reaction);
        await dbContext.SaveChangesAsync(ct);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/conversations/{conversationId}/messages/{messageId}/reactions/{emoji}", async (
            long conversationId, long messageId, string emoji,
            [FromServices] RemoveReactionHandler handler,
            CancellationToken ct) =>
        {
            var request = new RemoveReactionCommand(conversationId, messageId, Uri.UnescapeDataString(emoji));
            return await handler.ExecuteAsync(request, ct, _ => Results.NoContent());
        })
        .RequireAuthorization(Policies.User)
        .WithName("RemoveReaction").WithTags("Reactions");
}
