using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Forums.Posts;

public sealed record RemovePostTagCommand(
    long ChannelId,
    long ThreadId,
    long TagId
);

public sealed class RemovePostTagHandler(
    AppDbContext dbContext,
    IPermissionService permissionService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<RemovePostTagHandler> logger)
    : IRequestHandler<RemovePostTagCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemovePostTagCommand request, CancellationToken cancellationToken)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        var thread = await dbContext.Threads
            .Include(t => t.Channel)
            .FirstOrDefaultAsync(t => t.Id == request.ThreadId && t.ChannelId == request.ChannelId, cancellationToken);

        if (thread == null)
        {
            return Error.NotFound("THREAD_NOT_FOUND", "Forum post not found");
        }

        var firstMessage = await dbContext.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == thread.ConversationId)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var isAuthor = firstMessage?.AuthorId == userId;

        if (!isAuthor)
        {
            var permissionResult = await permissionService.EnsureChannelPermission(
                userId,
                request.ChannelId,
                Permission.ManageChannels);

            if (permissionResult.IsFailure)
            {
                return Error.Forbidden("INSUFFICIENT_PERMISSIONS", "Only the post author or channel moderators can remove tags");
            }
        }

        var postTag = await dbContext.ForumPostTags
            .FirstOrDefaultAsync(fpt => fpt.ThreadId == request.ThreadId && fpt.ForumTagId == request.TagId, cancellationToken);

        if (postTag == null)
        {
            return Error.NotFound("TAG_NOT_APPLIED", "Tag is not applied to this post");
        }

        dbContext.ForumPostTags.Remove(postTag);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} removed tag {TagId} from forum post {ThreadId}",
            userId, request.TagId, request.ThreadId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/channels/{channelId}/posts/{threadId}/tags/{tagId}", async (
            long channelId,
            long threadId,
            long tagId,
            [FromServices] RemovePostTagHandler handler,
            CancellationToken ct) =>
        {
            var command = new RemovePostTagCommand(
                ChannelId: channelId,
                ThreadId: threadId,
                TagId: tagId
            );

            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("RemovePostTag")
        .WithTags("Forums");
    }
}
