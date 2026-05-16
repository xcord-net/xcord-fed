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
using Xcord.Shared.Extensions;

namespace Xcord.Features.Forums;

public sealed record DeleteForumTagCommand(
    long ChannelId,
    long TagId
);

public sealed class DeleteForumTagHandler(
    AppDbContext dbContext,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    ILogger<DeleteForumTagHandler> logger)
    : IRequestHandler<DeleteForumTagCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteForumTagCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var forumTag = await dbContext.ForumTags
            .FirstOrDefaultAsync(ft => ft.Id == request.TagId && ft.ChannelId == request.ChannelId, cancellationToken);

        if (forumTag == null)
        {
            return Error.NotFound("TAG_NOT_FOUND", "Forum tag not found");
        }

        var permissionResult = await roleService.EnsureChannelRole(
            userId,
            request.ChannelId,
            Role.ManageChannels);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        forumTag.SoftDelete();

        var postTags = await dbContext.ForumPostTags
            .Where(fpt => fpt.ForumTagId == request.TagId)
            .ToListAsync(cancellationToken);

        dbContext.ForumPostTags.RemoveRange(postTags);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User {UserId} deleted forum tag {TagId}", userId, request.TagId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/channels/{channelId}/tags/{tagId}", async (
            long channelId,
            long tagId,
            [FromServices] DeleteForumTagHandler handler,
            CancellationToken ct) =>
        {
            var command = new DeleteForumTagCommand(
                ChannelId: channelId,
                TagId: tagId
            );

            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteForumTag")
        .WithTags("Forums");
    }
}
