using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using ThreadEntity = Xcord.Entities.Thread;

namespace Xcord.Features.Forums.Posts;

public sealed record CreateForumPostCommand(
    long ChannelId,
    string Title,
    string Content,
    List<string> Tags
);

public sealed record CreateForumPostResponse(
    long ThreadId,
    long ConversationId,
    long ChannelId,
    string Title,
    string AuthorUsername,
    long FirstMessageId,
    List<string> Tags,
    DateTimeOffset CreatedAt
);

public sealed class CreateForumPostHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IPermissionService permissionService,
    ICurrentUserService currentUserService,
    IOutboxWriter outboxWriter,
    ILogger<CreateForumPostHandler> logger)
    : IRequestHandler<CreateForumPostCommand, Result<CreateForumPostResponse>>
{
    public async Task<Result<CreateForumPostResponse>> Handle(CreateForumPostCommand request, CancellationToken cancellationToken)
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

        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == channel.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_MEMBER", "User is not a member of this server");
        }

        var sendMessagePermission = await permissionService.EnsureChannelPermission(
            userId,
            channel.Id,
            Permission.SendMessages);

        if (sendMessagePermission.IsFailure)
        {
            return sendMessagePermission.Error;
        }

        var createThreadPermission = await permissionService.EnsureChannelPermission(
            userId,
            channel.Id,
            Permission.CreatePublicThreads);

        if (createThreadPermission.IsFailure)
        {
            return createThreadPermission.Error;
        }

        var now = DateTimeOffset.UtcNow;

        var authorUsername = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var conversationId = snowflakeGenerator.NextId();
            var conversation = new Conversation
            {
                Id = conversationId,
                Type = ConversationType.Thread
            };

            dbContext.Conversations.Add(conversation);

            var threadId = snowflakeGenerator.NextId();
            var thread = new ThreadEntity
            {
                Id = threadId,
                ConversationId = conversationId,
                ChannelId = request.ChannelId,
                ParentMessageId = null,
                Title = request.Title,
                IsArchived = false,
                IsLocked = false,
                AutoArchiveDurationMinutes = channel.DefaultAutoArchiveDuration ?? 1440,
                LastActivityAt = DateTimeOffset.UtcNow,
                MessageCount = 1,
                CreatedAt = now
            };

            dbContext.Threads.Add(thread);

            var messageId = snowflakeGenerator.NextId();
            var message = new Message
            {
                Id = messageId,
                ConversationId = conversationId,
                AuthorId = userId,
                Type = MessageType.Default,
                Content = request.Content,
                CreatedAt = now
            };

            dbContext.Messages.Add(message);

            var threadMember = new ThreadMember
            {
                UserId = userId,
                ThreadId = threadId,
                JoinedAt = DateTimeOffset.UtcNow
            };

            dbContext.ThreadMembers.Add(threadMember);

            var appliedTagIds = new List<long>();
            foreach (var tagName in request.Tags)
            {
                var trimmedName = tagName.Trim();
                if (string.IsNullOrEmpty(trimmedName)) continue;

                // Find or create the forum tag for this channel
                var existingTag = await dbContext.ForumTags
                    .FirstOrDefaultAsync(ft => ft.ChannelId == request.ChannelId && ft.Name == trimmedName, cancellationToken);

                long tagId;
                if (existingTag != null)
                {
                    tagId = existingTag.Id;
                }
                else
                {
                    var newTag = new ForumTag
                    {
                        Id = snowflakeGenerator.NextId(),
                        ChannelId = request.ChannelId,
                        Name = trimmedName,
                        IsModerated = false,
                        Position = 0
                    };
                    dbContext.ForumTags.Add(newTag);
                    tagId = newTag.Id;
                }

                appliedTagIds.Add(tagId);
                var postTag = new ForumPostTag
                {
                    ThreadId = threadId,
                    ForumTagId = tagId
                };
                dbContext.ForumPostTags.Add(postTag);
            }

            await outboxWriter.WriteAsync(dbContext, "Forum.PostCreated", new
            {
                ThreadId = threadId,
                ChannelId = request.ChannelId,
                ServerId = channel.ServerId,
                AuthorId = userId,
                Title = request.Title,
                Tags = request.Tags,
                CreatedAt = now
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "User {UserId} created forum post {ThreadId} in channel {ChannelId}",
                userId, threadId, request.ChannelId);

            return new CreateForumPostResponse(
                ThreadId: threadId,
                ConversationId: conversationId,
                ChannelId: request.ChannelId,
                Title: request.Title,
                AuthorUsername: authorUsername,
                FirstMessageId: messageId,
                Tags: request.Tags,
                CreatedAt: now
            );
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/channels/{channelId}/posts", async (
            long channelId,
            CreateForumPostRequest request,
            IRequestHandler<CreateForumPostCommand, Result<CreateForumPostResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateForumPostCommand(
                ChannelId: channelId,
                Title: request.Title,
                Content: request.Content,
                Tags: request.Tags ?? []
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateForumPost")
        .WithTags("Forums");
    }
}

public sealed record CreateForumPostRequest(
    string Title,
    string Content,
    List<string>? Tags = null
);
