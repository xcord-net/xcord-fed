using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Forums;

public sealed record UpdateForumTagCommand(
    long ChannelId,
    long TagId,
    string? Name = null,
    string? EmojiUnicode = null,
    long? EmojiId = null,
    bool? IsModerated = null,
    int? Position = null
);

public sealed record UpdateForumTagResponse(
    long Id,
    long ChannelId,
    string Name,
    string? EmojiUnicode,
    long? EmojiId,
    bool IsModerated,
    int Position
);

public sealed class UpdateForumTagHandler(
    AppDbContext dbContext,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    ILogger<UpdateForumTagHandler> logger)
    : IRequestHandler<UpdateForumTagCommand, Result<UpdateForumTagResponse>>, IValidatable<UpdateForumTagCommand>
{
    public Error? Validate(UpdateForumTagCommand request)
    {
        if (request.Name != null && (request.Name.Length < 1 || request.Name.Length > 20))
        {
            return Error.Validation("VALIDATION_ERROR", "Tag name must be between 1 and 20 characters");
        }

        return null;
    }

    public async Task<Result<UpdateForumTagResponse>> Handle(UpdateForumTagCommand request, CancellationToken cancellationToken)
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

        if (request.Name != null)
            forumTag.Name = request.Name;
        if (request.EmojiUnicode != null)
            forumTag.EmojiUnicode = request.EmojiUnicode;
        if (request.EmojiId.HasValue)
            forumTag.EmojiId = request.EmojiId;
        if (request.IsModerated.HasValue)
            forumTag.IsModerated = request.IsModerated.Value;
        if (request.Position.HasValue)
            forumTag.Position = request.Position.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} updated forum tag {TagId}", userId, request.TagId);

        return new UpdateForumTagResponse(
            Id: forumTag.Id,
            ChannelId: forumTag.ChannelId,
            Name: forumTag.Name,
            EmojiUnicode: forumTag.EmojiUnicode,
            EmojiId: forumTag.EmojiId,
            IsModerated: forumTag.IsModerated,
            Position: forumTag.Position
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/channels/{channelId}/tags/{tagId}", async (
            long channelId,
            long tagId,
            UpdateForumTagRequest request,
            IRequestHandler<UpdateForumTagCommand, Result<UpdateForumTagResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new UpdateForumTagCommand(
                ChannelId: channelId,
                TagId: tagId,
                Name: request.Name,
                EmojiUnicode: request.EmojiUnicode,
                EmojiId: request.EmojiId,
                IsModerated: request.IsModerated,
                Position: request.Position
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateForumTag")
        .WithTags("Forums");
    }
}

public sealed record UpdateForumTagRequest(
    string? Name = null,
    string? EmojiUnicode = null,
    long? EmojiId = null,
    bool? IsModerated = null,
    int? Position = null
);
