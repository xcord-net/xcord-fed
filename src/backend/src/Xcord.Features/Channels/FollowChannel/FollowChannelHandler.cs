using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

public sealed record FollowChannelRequest(long TargetChannelId);

public sealed record FollowChannelCommand(long ServerId, long ChannelId, long TargetChannelId);

public sealed record FollowChannelResponse(
    long Id,
    long SourceChannelId,
    long TargetChannelId,
    long SourceServerId,
    long TargetServerId,
    DateTimeOffset CreatedAt
);

public sealed class FollowChannelHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService)
    : IRequestHandler<FollowChannelCommand, Result<FollowChannelResponse>>,
      IValidatable<FollowChannelCommand>
{
    public Error? Validate(FollowChannelCommand request)
    {
        if (request.TargetChannelId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Target channel ID is required");
        }

        if (request.TargetChannelId == request.ChannelId)
        {
            return Error.Validation("VALIDATION_ERROR", "A channel cannot follow itself");
        }

        return null;
    }

    public async Task<Result<FollowChannelResponse>> Handle(
        FollowChannelCommand request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Load the source channel
        var sourceChannel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId && c.ServerId == request.ServerId, cancellationToken);

        if (sourceChannel is null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Source channel not found");
        }

        // Source must be an announcement channel
        if (sourceChannel.Type != ChannelType.Announcement)
        {
            return Error.Validation("NOT_ANNOUNCEMENT", "Only announcement channels can be followed");
        }

        // Load the target channel
        var targetChannel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.TargetChannelId, cancellationToken);

        if (targetChannel is null)
        {
            return Error.NotFound("TARGET_NOT_FOUND", "Target channel not found");
        }

        // Caller must be a member of the source server
        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == request.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_A_MEMBER", "You must be a member of this server to follow its channels");
        }

        // Check for duplicate subscription
        var existing = await dbContext.CrosspostSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(cs =>
                cs.SourceChannelId == request.ChannelId &&
                cs.TargetChannelId == request.TargetChannelId, cancellationToken);

        if (existing is not null)
        {
            return Error.Validation("ALREADY_FOLLOWING", "Already following this channel");
        }

        var now = DateTimeOffset.UtcNow;
        var subscription = new CrosspostSubscription
        {
            Id = snowflakeGenerator.NextId(),
            SourceChannelId = request.ChannelId,
            TargetChannelId = request.TargetChannelId,
            SourceServerId = request.ServerId,
            TargetServerId = targetChannel.ServerId,
            CreatedAt = now
        };

        dbContext.CrosspostSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new FollowChannelResponse(
            Id: subscription.Id,
            SourceChannelId: subscription.SourceChannelId,
            TargetChannelId: subscription.TargetChannelId,
            SourceServerId: subscription.SourceServerId,
            TargetServerId: subscription.TargetServerId,
            CreatedAt: subscription.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/servers/{serverId:long}/channels/{channelId:long}/followers", async (
            [FromRoute] long serverId,
            [FromRoute] long channelId,
            [FromBody] FollowChannelRequest request,
            [FromServices] FollowChannelHandler handler,
            CancellationToken ct) =>
        {
            var command = new FollowChannelCommand(serverId, channelId, request.TargetChannelId);
            return await handler.ExecuteAsync(command, ct,
                success => Results.Created($"/api/v1/servers/{serverId}/channels/{channelId}/followers/{success.Id}", success));
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("FollowChannel")
        .WithTags("Channels");
}
