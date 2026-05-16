using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.ScheduledMessages;

public sealed record ListScheduledMessagesRequest(
    long ChannelId
);

public sealed record ScheduledMessageResponse(
    long Id,
    long ConversationId,
    long AuthorId,
    string Content,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? SentAt
);

public sealed class ListScheduledMessagesHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListScheduledMessagesRequest, Result<IReadOnlyList<ScheduledMessageResponse>>>
{
    public async Task<Result<IReadOnlyList<ScheduledMessageResponse>>> Handle(
        ListScheduledMessagesRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Resolve channel
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");

        // Return only the requesting user's pending (unsent, non-deleted) scheduled messages,
        // ordered by ScheduledAt ascending. The global soft-delete query filter already excludes
        // DeletedAt != null rows, so we only need to filter SentAt == null explicitly.
        var messages = await dbContext.ScheduledMessages
            .AsNoTracking()
            .Where(sm => sm.ConversationId == channel.ConversationId
                         && sm.AuthorId == userId
                         && sm.SentAt == null)
            .OrderBy(sm => sm.ScheduledAt)
            .Select(sm => new ScheduledMessageResponse(
                sm.Id,
                sm.ConversationId,
                sm.AuthorId,
                sm.Content,
                sm.ScheduledAt,
                sm.SentAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ScheduledMessageResponse>>.Success(messages);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/channels/{channelId}/scheduled-messages", async (
            long channelId,
            [FromServices] ListScheduledMessagesHandler handler,
            CancellationToken ct) =>
        {
            var request = new ListScheduledMessagesRequest(ChannelId: channelId);
            return await handler.ExecuteAsync(request, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListScheduledMessages")
        .WithTags("ScheduledMessages");
    }
}
