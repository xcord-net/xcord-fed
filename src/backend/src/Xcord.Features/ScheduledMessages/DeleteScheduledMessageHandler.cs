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
using Xcord.Shared.Extensions;

namespace Xcord.Features.ScheduledMessages;

public sealed record DeleteScheduledMessageRequest(
    long ChannelId,
    long ScheduledMessageId
);

public sealed class DeleteScheduledMessageHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<DeleteScheduledMessageHandler> logger)
    : IRequestHandler<DeleteScheduledMessageRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        DeleteScheduledMessageRequest request, CancellationToken cancellationToken)
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

        // Find the scheduled message scoped to this conversation
        var scheduledMessage = await dbContext.ScheduledMessages
            .FirstOrDefaultAsync(sm => sm.Id == request.ScheduledMessageId
                                       && sm.ConversationId == channel.ConversationId,
                                 cancellationToken);

        if (scheduledMessage == null)
            return Error.NotFound("SCHEDULED_MESSAGE_NOT_FOUND", "Scheduled message not found");

        // Only the author can cancel their own scheduled message
        if (scheduledMessage.AuthorId != userId)
            return Error.Forbidden("CANNOT_DELETE_SCHEDULED_MESSAGE", "You can only cancel your own scheduled messages");

        // Soft delete
        scheduledMessage.SoftDelete();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} cancelled scheduled message {ScheduledMessageId} in channel {ChannelId}",
            userId, request.ScheduledMessageId, request.ChannelId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/channels/{channelId}/scheduled-messages/{id}", async (
            long channelId,
            long id,
            [FromServices] DeleteScheduledMessageHandler handler,
            CancellationToken ct) =>
        {
            var request = new DeleteScheduledMessageRequest(
                ChannelId: channelId,
                ScheduledMessageId: id
            );

            return await handler.ExecuteAsync(request, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteScheduledMessage")
        .WithTags("ScheduledMessages");
    }
}
