using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Messages.SuperReactions;

public sealed record AddSuperReactionCommand(long ConversationId, long MessageId, string Emoji);
public sealed record SuperReactionResponse(long MessageId, long UserId, string Emoji, bool IsSuper, DateTimeOffset CreatedAt);

public sealed class AddSuperReactionHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<AddSuperReactionCommand, Result<SuperReactionResponse>>
{
    public async Task<Result<SuperReactionResponse>> Handle(AddSuperReactionCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var message = await dbContext.Messages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MessageId && m.ConversationId == request.ConversationId, ct);
        if (message == null) return Error.NotFound("MESSAGE_NOT_FOUND", "Message not found");

        var existing = await dbContext.Reactions
            .FirstOrDefaultAsync(r => r.MessageId == request.MessageId && r.UserId == userId && r.Emoji == request.Emoji, ct);

        var now = DateTimeOffset.UtcNow;
        if (existing != null)
        {
            existing.IsSuper = true;
            await dbContext.SaveChangesAsync(ct);
            return new SuperReactionResponse(existing.MessageId, existing.UserId, existing.Emoji, true, existing.CreatedAt);
        }

        var reaction = new Reaction
        {
            MessageId = request.MessageId, UserId = userId,
            Emoji = request.Emoji, IsSuper = true, CreatedAt = now
        };
        dbContext.Reactions.Add(reaction);
        await dbContext.SaveChangesAsync(ct);

        return new SuperReactionResponse(reaction.MessageId, userId, reaction.Emoji, true, now);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/conversations/{conversationId}/messages/{messageId}/super-reactions/{emoji}", async (
            long conversationId, long messageId, string emoji,
            IRequestHandler<AddSuperReactionCommand, Result<SuperReactionResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new AddSuperReactionCommand(conversationId, messageId, Uri.UnescapeDataString(emoji)), ct))
        .RequireAuthorization(Policies.User)
        .WithName("AddSuperReaction").WithTags("SuperReactions");
}
