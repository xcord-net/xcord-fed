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
using ThreadEntity = Xcord.Entities.Thread;

namespace Xcord.Features.Threads;

public sealed record CreateThreadRequest(
    long ChannelId,
    long? ParentMessageId,
    string? Title,
    int AutoArchiveDurationMinutes = 1440
);

public sealed record CreateThreadResponse(
    long Id,
    long ConversationId,
    long ChannelId,
    long? ParentMessageId,
    string? Title,
    bool IsArchived,
    bool IsLocked,
    int AutoArchiveDurationMinutes,
    DateTimeOffset LastActivityAt,
    int MessageCount,
    int MemberCount,
    DateTimeOffset CreatedAt
);

public sealed class CreateThreadHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    ILogger<CreateThreadHandler> logger)
    : IRequestHandler<CreateThreadRequest, Result<CreateThreadResponse>>, IValidatable<CreateThreadRequest>
{
    private static readonly int[] ValidDurations = { 60, 1440, 4320, 10080 };

    public Error? Validate(CreateThreadRequest request)
    {
        // Title is required if ParentMessageId is null (forum post)
        if (!request.ParentMessageId.HasValue && string.IsNullOrWhiteSpace(request.Title))
            return Error.Validation("VALIDATION_FAILED", "Title is required for forum posts");

        // Title max length
        if (!string.IsNullOrEmpty(request.Title) && request.Title.Length > 100)
            return Error.Validation("VALIDATION_FAILED", "Title cannot exceed 100 characters");

        // AutoArchiveDurationMinutes must be one of the valid values
        if (!ValidDurations.Contains(request.AutoArchiveDurationMinutes))
            return Error.Validation("VALIDATION_FAILED", $"AutoArchiveDurationMinutes must be one of: {string.Join(", ", ValidDurations)}");

        return null;
    }

    public async Task<Result<CreateThreadResponse>> Handle(CreateThreadRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get channel and verify it exists
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        // Verify user is a member of the server
        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == channel.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_MEMBER", "User is not a member of this server");
        }

        // Check CreatePublicThreads permission
        var permissionResult = await roleService.EnsureChannelRole(
            userId,
            channel.Id,
            Role.CreatePublicThreads);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // If ParentMessageId is provided, verify the message exists in this channel
        if (request.ParentMessageId.HasValue)
        {
            var parentMessage = await dbContext.Messages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == request.ParentMessageId.Value, cancellationToken);

            if (parentMessage == null)
            {
                return Error.NotFound("PARENT_MESSAGE_NOT_FOUND", "Parent message not found");
            }

            // Verify parent message is in the same channel
            if (parentMessage.ConversationId != channel.ConversationId)
            {
                return Error.Validation("INVALID_PARENT_MESSAGE", "Parent message does not belong to this channel");
            }
        }

        var now = DateTimeOffset.UtcNow;

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Create Conversation for the thread
            var conversationId = snowflakeGenerator.NextId();
            var conversation = new Conversation
            {
                Id = conversationId,
                Type = ConversationType.Thread
            };

            dbContext.Conversations.Add(conversation);

            // Create Thread
            var threadId = snowflakeGenerator.NextId();
            var thread = new ThreadEntity
            {
                Id = threadId,
                ConversationId = conversationId,
                ChannelId = request.ChannelId,
                ParentMessageId = request.ParentMessageId,
                Title = request.Title,
                IsArchived = false,
                IsLocked = false,
                AutoArchiveDurationMinutes = request.AutoArchiveDurationMinutes,
                LastActivityAt = DateTimeOffset.UtcNow,
                MessageCount = 0,
                CreatedAt = now
            };

            dbContext.Threads.Add(thread);

            // Auto-add creator as ThreadMember
            var threadMember = new ThreadMember
            {
                UserId = userId,
                ThreadId = threadId,
                JoinedAt = DateTimeOffset.UtcNow
            };

            dbContext.ThreadMembers.Add(threadMember);

            // If ParentMessageId is provided, create a system message in the parent channel's conversation
            if (request.ParentMessageId.HasValue)
            {
                var systemMessageId = snowflakeGenerator.NextId();
                var systemMessage = new Message
                {
                    Id = systemMessageId,
                    ConversationId = channel.ConversationId,
                    AuthorId = null, // System message
                    Type = MessageType.ThreadCreated,
                    Content = string.Empty,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        ThreadId = threadId,
                        ThreadTitle = request.Title,
                        ParentMessageId = request.ParentMessageId.Value
                    }),
                    CreatedAt = now
                };

                dbContext.Messages.Add(systemMessage);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "User {UserId} created thread {ThreadId} in channel {ChannelId}",
                userId, threadId, request.ChannelId);

            return new CreateThreadResponse(
                Id: thread.Id,
                ConversationId: thread.ConversationId,
                ChannelId: thread.ChannelId,
                ParentMessageId: thread.ParentMessageId,
                Title: thread.Title,
                IsArchived: thread.IsArchived,
                IsLocked: thread.IsLocked,
                AutoArchiveDurationMinutes: thread.AutoArchiveDurationMinutes,
                LastActivityAt: thread.LastActivityAt,
                MessageCount: thread.MessageCount,
                MemberCount: 1, // Creator is auto-joined
                CreatedAt: thread.CreatedAt
            );
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/channels/{channelId}/threads", async (
            long channelId,
            [FromBody] CreateThreadBodyRequest bodyRequest,
            [FromServices] CreateThreadHandler handler,
            CancellationToken ct) =>
        {
            var request = new CreateThreadRequest(
                ChannelId: channelId,
                ParentMessageId: bodyRequest.ParentMessageId,
                Title: bodyRequest.Title,
                AutoArchiveDurationMinutes: bodyRequest.AutoArchiveDurationMinutes ?? 1440
            );

            return await handler.ExecuteAsync(request, ct,
                success => Results.Created($"/api/v1/channels/{channelId}/threads/{success.Id}", success));
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateThread")
        .WithTags("Threads");
    }
}

public sealed record CreateThreadBodyRequest(
    long? ParentMessageId,
    string? Title,
    int? AutoArchiveDurationMinutes = 1440
);
