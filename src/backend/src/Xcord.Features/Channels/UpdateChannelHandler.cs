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

namespace Xcord.Features.Channels;

public sealed record UpdateChannelCommand(
    long ChannelId,
    string? Name = null,
    string? Topic = null,
    int? Position = null,
    long? CategoryId = null,
    int? SlowModeSeconds = null,
    bool? IsNsfw = null,
    ForumSort? DefaultSortOrder = null,
    bool? RequireTag = null,
    int? DefaultAutoArchiveDuration = null
);

public sealed record UpdateChannelResponse(
    long Id,
    long ConversationId,
    long ServerId,
    long? CategoryId,
    string Name,
    string? Topic,
    ChannelType Type,
    int Position,
    int? SlowModeSeconds,
    bool IsNsfw,
    ForumSort? DefaultSortOrder,
    bool RequireTag,
    int? DefaultAutoArchiveDuration,
    DateTimeOffset CreatedAt
);

public sealed record UpdateChannelRequest(
    string? Name = null,
    string? Topic = null,
    int? Position = null,
    long? CategoryId = null,
    int? SlowModeSeconds = null,
    bool? IsNsfw = null,
    ForumSort? DefaultSortOrder = null,
    bool? RequireTag = null,
    int? DefaultAutoArchiveDuration = null
);

public sealed class UpdateChannelHandler(
    AppDbContext dbContext,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    ILogger<UpdateChannelHandler> logger)
    : IRequestHandler<UpdateChannelCommand, Result<UpdateChannelResponse>>, IValidatable<UpdateChannelCommand>
{
    public Error? Validate(UpdateChannelCommand request)
    {
        if (request.Name != null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Error.Validation("VALIDATION_ERROR", "Channel name cannot be empty");
            }

            if (request.Name.Length > 100)
            {
                return Error.Validation("VALIDATION_ERROR", "Channel name must not exceed 100 characters");
            }
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

        if (request.Position.HasValue && request.Position.Value < 0)
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

    public async Task<Result<UpdateChannelResponse>> Handle(UpdateChannelCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get channel
        var channel = await dbContext.Channels
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        // Check ManageChannels permission
        var permissionResult = await roleService.EnsureServerRole(
            userId,
            channel.ServerId,
            Role.ManageChannels);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // If category is being changed, verify it exists and belongs to this server
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await dbContext.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.CategoryId.Value && c.ServerId == channel.ServerId, cancellationToken);

            if (!categoryExists)
            {
                return Error.NotFound("CATEGORY_NOT_FOUND", "Category not found or does not belong to this server");
            }
        }

        // Apply updates
        if (request.Name != null) channel.Name = request.Name;
        if (request.Topic != null) channel.Topic = request.Topic;
        if (request.Position.HasValue) channel.Position = request.Position.Value;
        if (request.CategoryId.HasValue) channel.CategoryId = request.CategoryId.Value;
        if (request.SlowModeSeconds.HasValue) channel.SlowModeSeconds = request.SlowModeSeconds.Value;
        if (request.IsNsfw.HasValue) channel.IsNsfw = request.IsNsfw.Value;
        if (request.DefaultSortOrder.HasValue) channel.DefaultSortOrder = request.DefaultSortOrder.Value;
        if (request.RequireTag.HasValue) channel.RequireTag = request.RequireTag.Value;
        if (request.DefaultAutoArchiveDuration.HasValue) channel.DefaultAutoArchiveDuration = request.DefaultAutoArchiveDuration.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} updated channel {ChannelId} in server {ServerId}",
            userId, request.ChannelId, channel.ServerId);

        return new UpdateChannelResponse(
            Id: channel.Id,
            ConversationId: channel.ConversationId,
            ServerId: channel.ServerId,
            CategoryId: channel.CategoryId,
            Name: channel.Name,
            Topic: channel.Topic,
            Type: channel.Type,
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
        return app.MapPatch("/api/v1/channels/{channelId}", async (
            long channelId,
            [FromBody] UpdateChannelRequest request,
            [FromServices] UpdateChannelHandler handler,
            CancellationToken ct) =>
        {
            var command = new UpdateChannelCommand(
                ChannelId: channelId,
                Name: request.Name,
                Topic: request.Topic,
                Position: request.Position,
                CategoryId: request.CategoryId,
                SlowModeSeconds: request.SlowModeSeconds,
                IsNsfw: request.IsNsfw,
                DefaultSortOrder: request.DefaultSortOrder,
                RequireTag: request.RequireTag,
                DefaultAutoArchiveDuration: request.DefaultAutoArchiveDuration
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateChannel")
        .WithTags("Channels");
    }
}
