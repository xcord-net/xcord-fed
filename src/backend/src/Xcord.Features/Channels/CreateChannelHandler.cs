using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

public sealed record CreateChannelCommand(
    long ServerId,
    string Name,
    ChannelType Type,
    long? CategoryId = null,
    string? Topic = null,
    int Position = 0,
    int? SlowModeSeconds = null,
    bool IsNsfw = false,
    ForumSort? DefaultSortOrder = null,
    bool RequireTag = false,
    int? DefaultAutoArchiveDuration = null,
    ChannelCapability Capabilities = ChannelCapability.None,
    long? AccessGroupId = null
);

public sealed record CreateChannelResponse(
    long Id,
    long ConversationId,
    long ServerId,
    long? CategoryId,
    string Name,
    string? Topic,
    ChannelType Type,
    ChannelCapability Capabilities,
    long? AccessGroupId,
    int Position,
    int? SlowModeSeconds,
    bool IsNsfw,
    ForumSort? DefaultSortOrder,
    bool RequireTag,
    int? DefaultAutoArchiveDuration,
    DateTimeOffset CreatedAt
);

public sealed record CreateChannelRequest(
    string Name,
    ChannelType Type,
    long? CategoryId = null,
    string? Topic = null,
    int Position = 0,
    int? SlowModeSeconds = null,
    bool IsNsfw = false,
    ForumSort? DefaultSortOrder = null,
    bool RequireTag = false,
    int? DefaultAutoArchiveDuration = null,
    ChannelCapability Capabilities = ChannelCapability.None,
    long? AccessGroupId = null
);

public sealed class CreateChannelHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    IOptions<TierOptions> tierOptions,
    ILogger<CreateChannelHandler> logger)
    : IRequestHandler<CreateChannelCommand, Result<CreateChannelResponse>>, IValidatable<CreateChannelCommand>
{
    public Error? Validate(CreateChannelCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("VALIDATION_ERROR", "Channel name is required");
        }

        if (request.Name.Length > 100)
        {
            return Error.Validation("VALIDATION_ERROR", "Channel name must not exceed 100 characters");
        }

        if (request.Topic != null && request.Topic.Length > 1024)
        {
            return Error.Validation("VALIDATION_ERROR", "Channel topic must not exceed 1024 characters");
        }

        if (request.SlowModeSeconds.HasValue)
        {
            if (request.SlowModeSeconds.Value < 0)
            {
                return Error.Validation("VALIDATION_ERROR", "Slow mode seconds must be non-negative");
            }

            if (request.SlowModeSeconds.Value > 21600)
            {
                return Error.Validation("VALIDATION_ERROR", "Slow mode seconds must not exceed 21600 (6 hours)");
            }
        }

        if (request.Position < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Position must be non-negative");
        }

        if (request.DefaultAutoArchiveDuration.HasValue)
        {
            var duration = request.DefaultAutoArchiveDuration.Value;
            if (duration != 60 && duration != 1440 && duration != 4320 && duration != 10080)
            {
                return Error.Validation("VALIDATION_ERROR", "Auto archive duration must be 60, 1440, 4320, or 10080 minutes");
            }
        }

        return null;
    }

    public async Task<Result<CreateChannelResponse>> Handle(CreateChannelCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Resolve capabilities and type from each other for backward compatibility.
        // If Capabilities is provided (non-zero), use it and derive Type from the primary capability.
        // If only Type is provided, derive Capabilities from Type.
        ChannelType resolvedType;
        ChannelCapability resolvedCapabilities;
        if (request.Capabilities != ChannelCapability.None)
        {
            resolvedCapabilities = request.Capabilities;
            resolvedType = resolvedCapabilities.HasFlag(ChannelCapability.Forum) ? ChannelType.Forum
                : resolvedCapabilities.HasFlag(ChannelCapability.Announcement) ? ChannelType.Announcement
                : resolvedCapabilities.HasFlag(ChannelCapability.Voice) ? ChannelType.Voice
                : ChannelType.Text;
        }
        else
        {
            resolvedType = request.Type;
            resolvedCapabilities = resolvedType switch
            {
                ChannelType.Voice => ChannelCapability.Voice | ChannelCapability.Video,
                ChannelType.Forum => ChannelCapability.Forum | ChannelCapability.Chat,
                ChannelType.Announcement => ChannelCapability.Announcement | ChannelCapability.Chat,
                _ => ChannelCapability.Chat
            };
        }

        // Tier gating: reject voice channel creation when feature is disabled
        if (resolvedCapabilities.HasFlag(ChannelCapability.Voice) && !tierOptions.Value.CanUseVoiceChannels)
        {
            return Error.Forbidden("FEATURE_DISABLED", "Voice channels are not available on your current plan");
        }

        // Check if server exists
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Check ManageChannels permission
        var permissionResult = await roleService.EnsureServerRole(
            userId,
            request.ServerId,
            Role.ManageChannels);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // If category is specified, verify it exists and belongs to this server
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await dbContext.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.CategoryId.Value && c.ServerId == request.ServerId, cancellationToken);

            if (!categoryExists)
            {
                return Error.NotFound("CATEGORY_NOT_FOUND", "Category not found or does not belong to this server");
            }
        }

        var now = DateTimeOffset.UtcNow;

        // Create Conversation
        var conversationId = snowflakeGenerator.NextId();
        var conversation = new Conversation
        {
            Id = conversationId,
            Type = ConversationType.Channel
        };

        dbContext.Conversations.Add(conversation);

        // Create Channel
        var channelId = snowflakeGenerator.NextId();
        var channel = new Channel
        {
            Id = channelId,
            ConversationId = conversationId,
            ServerId = request.ServerId,
            CategoryId = request.CategoryId,
            Name = request.Name,
            Topic = request.Topic,
            Type = resolvedType,
            Capabilities = resolvedCapabilities,
            AccessGroupId = request.AccessGroupId,
            Position = request.Position,
            SlowModeSeconds = request.SlowModeSeconds,
            IsNsfw = request.IsNsfw,
            DefaultSortOrder = request.DefaultSortOrder,
            RequireTag = request.RequireTag,
            DefaultAutoArchiveDuration = request.DefaultAutoArchiveDuration,
            CreatedAt = now
        };

        dbContext.Channels.Add(channel);

        // Create ReadState rows for all server members so the unread notification
        // system can track messages in the new channel.
        if (resolvedCapabilities.HasFlag(ChannelCapability.Chat))
        {
            var memberUserIds = await dbContext.ServerMembers
                .AsNoTracking()
                .Where(sm => sm.ServerId == request.ServerId)
                .Select(sm => sm.UserId)
                .ToListAsync(cancellationToken);

            foreach (var memberUserId in memberUserIds)
            {
                dbContext.ReadStates.Add(new Xcord.Entities.ReadState
                {
                    UserId = memberUserId,
                    ConversationId = conversationId,
                    UnreadCount = 0,
                    MentionCount = 0
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Notify all server members after save
        await notificationService.NotifyServerAsync(request.ServerId, "Chat_ChannelCreated", new
        {
            serverId = request.ServerId,
            id = channel.Id,
            conversationId = channel.ConversationId,
            categoryId = channel.CategoryId,
            name = channel.Name,
            topic = channel.Topic,
            type = channel.Type.ToString(),
            position = channel.Position,
            slowModeSeconds = channel.SlowModeSeconds,
            isNsfw = channel.IsNsfw,
            createdAt = channel.CreatedAt
        });

        logger.LogInformation(
            "User {UserId} created channel {ChannelName} (ID: {ChannelId}) in server {ServerId}",
            userId, channel.Name, channelId, request.ServerId);

        return new CreateChannelResponse(
            Id: channel.Id,
            ConversationId: channel.ConversationId,
            ServerId: channel.ServerId,
            CategoryId: channel.CategoryId,
            Name: channel.Name,
            Topic: channel.Topic,
            Type: channel.Type,
            Capabilities: channel.Capabilities,
            AccessGroupId: channel.AccessGroupId,
            Position: channel.Position,
            SlowModeSeconds: channel.SlowModeSeconds,
            IsNsfw: channel.IsNsfw,
            DefaultSortOrder: channel.DefaultSortOrder,
            RequireTag: channel.RequireTag,
            DefaultAutoArchiveDuration: channel.DefaultAutoArchiveDuration,
            CreatedAt: channel.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/channels", async (
            long serverId,
            [FromBody] CreateChannelRequest request,
            [FromServices] CreateChannelHandler handler,
            CancellationToken ct) =>
        {
            var command = new CreateChannelCommand(
                ServerId: serverId,
                Name: request.Name,
                Type: request.Type,
                CategoryId: request.CategoryId,
                Topic: request.Topic,
                Position: request.Position,
                SlowModeSeconds: request.SlowModeSeconds,
                IsNsfw: request.IsNsfw,
                DefaultSortOrder: request.DefaultSortOrder,
                RequireTag: request.RequireTag,
                DefaultAutoArchiveDuration: request.DefaultAutoArchiveDuration,
                Capabilities: request.Capabilities,
                AccessGroupId: request.AccessGroupId
            );

            return await handler.ExecuteAsync(command, ct, success => Results.Created($"/api/v1/channels/{success.Id}", success));
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateChannel")
        .WithTags("Channels");
    }
}
