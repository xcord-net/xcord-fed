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

public sealed record CreateForumTagCommand(
    long ChannelId,
    string Name,
    string? EmojiUnicode = null,
    long? EmojiId = null,
    bool IsModerated = false,
    int Position = 0
);

public sealed record CreateForumTagResponse(
    long Id,
    long ChannelId,
    string Name,
    string? EmojiUnicode,
    long? EmojiId,
    bool IsModerated,
    int Position
);

public sealed class CreateForumTagHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IPermissionService permissionService,
    ICurrentUserService currentUserService,
    ILogger<CreateForumTagHandler> logger)
    : IRequestHandler<CreateForumTagCommand, Result<CreateForumTagResponse>>, IValidatable<CreateForumTagCommand>
{
    public Error? Validate(CreateForumTagCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("VALIDATION_ERROR", "Tag name is required");
        }

        if (request.Name.Length < 1 || request.Name.Length > 20)
        {
            return Error.Validation("VALIDATION_ERROR", "Tag name must be between 1 and 20 characters");
        }

        return null;
    }

    public async Task<Result<CreateForumTagResponse>> Handle(CreateForumTagCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        if (channel.Type != ChannelType.Forum)
        {
            return Error.Validation("NOT_FORUM_CHANNEL", "Channel is not a forum channel");
        }

        var permissionResult = await permissionService.EnsureChannelPermission(
            userId,
            request.ChannelId,
            Permission.ManageChannels);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        var tagCount = await dbContext.ForumTags
            .CountAsync(ft => ft.ChannelId == request.ChannelId, cancellationToken);

        if (tagCount >= 20)
        {
            return Error.Validation("MAX_TAGS_REACHED", "Forum channel can have a maximum of 20 tags");
        }

        var tagId = snowflakeGenerator.NextId();
        var forumTag = new ForumTag
        {
            Id = tagId,
            ChannelId = request.ChannelId,
            Name = request.Name,
            EmojiUnicode = request.EmojiUnicode,
            EmojiId = request.EmojiId,
            IsModerated = request.IsModerated,
            Position = request.Position
        };

        dbContext.ForumTags.Add(forumTag);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created forum tag {TagName} (ID: {TagId}) in channel {ChannelId}",
            userId, forumTag.Name, tagId, request.ChannelId);

        return new CreateForumTagResponse(
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
        return app.MapPost("/api/v1/channels/{channelId}/tags", async (
            long channelId,
            CreateForumTagRequest request,
            IRequestHandler<CreateForumTagCommand, Result<CreateForumTagResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateForumTagCommand(
                ChannelId: channelId,
                Name: request.Name,
                EmojiUnicode: request.EmojiUnicode,
                EmojiId: request.EmojiId,
                IsModerated: request.IsModerated,
                Position: request.Position
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateForumTag")
        .WithTags("Forums");
    }
}

public sealed record CreateForumTagRequest(
    string Name,
    string? EmojiUnicode = null,
    long? EmojiId = null,
    bool IsModerated = false,
    int Position = 0
);
