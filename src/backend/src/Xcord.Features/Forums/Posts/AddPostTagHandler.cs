using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Forums;

public sealed record AddPostTagCommand(
    long ChannelId,
    long ThreadId,
    long TagId
);

public sealed class AddPostTagHandler(
    AppDbContext dbContext,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    ILogger<AddPostTagHandler> logger)
    : IRequestHandler<AddPostTagCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(AddPostTagCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

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
            var permissionResult = await roleService.EnsureChannelRole(
                userId,
                request.ChannelId,
                Role.ManageChannels);

            if (permissionResult.IsFailure)
            {
                return Error.Forbidden("INSUFFICIENT_PERMISSIONS", "Only the post author or channel moderators can add tags");
            }
        }

        var tag = await dbContext.ForumTags
            .AsNoTracking()
            .FirstOrDefaultAsync(ft => ft.Id == request.TagId && ft.ChannelId == request.ChannelId, cancellationToken);

        if (tag == null)
        {
            return Error.NotFound("TAG_NOT_FOUND", "Forum tag not found");
        }

        var existingPostTag = await dbContext.ForumPostTags
            .AsNoTracking()
            .AnyAsync(fpt => fpt.ThreadId == request.ThreadId && fpt.ForumTagId == request.TagId, cancellationToken);

        if (existingPostTag)
        {
            return Error.Validation("TAG_ALREADY_APPLIED", "Tag is already applied to this post");
        }

        var currentTagCount = await dbContext.ForumPostTags
            .CountAsync(fpt => fpt.ThreadId == request.ThreadId, cancellationToken);

        if (currentTagCount >= 5)
        {
            return Error.Validation("MAX_TAGS_REACHED", "A maximum of 5 tags can be applied to a forum post");
        }

        var postTag = new ForumPostTag
        {
            ThreadId = request.ThreadId,
            ForumTagId = request.TagId
        };

        dbContext.ForumPostTags.Add(postTag);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} added tag {TagId} to forum post {ThreadId}",
            userId, request.TagId, request.ThreadId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPut("/api/v1/channels/{channelId}/posts/{threadId}/tags/{tagId}", async (
            long channelId,
            long threadId,
            long tagId,
            [FromServices] AddPostTagHandler handler,
            CancellationToken ct) =>
        {
            var command = new AddPostTagCommand(
                ChannelId: channelId,
                ThreadId: threadId,
                TagId: tagId
            );

            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("AddPostTag")
        .WithTags("Forums");
    }
}
